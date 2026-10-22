using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Proyectos.Crear;

namespace App.Application.Novedades.Sincronizacion;

/// <summary>
/// Sincronización manual de PERMISO MEDICO (TAREA-23-3, N12). R3: la fuente, la base y (solo si hace falta) la API de
/// empleados se leen FUERA de la transacción; dentro solo se escribe, en lotes de 100 (P3), cada uno con su propia
/// ITransaccionAsignaciones (la alta puntual escribe Empleado bajo ese bloqueo). Lo que no cambia no entra a ninguna
/// transacción: sincronizar dos veces seguidas no escribe nada. Si un lote falla, los anteriores quedan confirmados
/// (<see cref="Parcial"/> para el log) y repetir la sincronización completa el resto.
/// </summary>
public sealed class SincronizacionPermisosMedicosServicio(
    IFuenteNovedadesExternas fuente,
    ISincronizacionNovedades sincronizacion,
    IDatosReferenciaProyecto datos,
    IFuenteEmpleadosErp empleadosErp,
    ITransaccionAsignaciones transaccion,
    TimeProvider reloj)
{
    public const int TamanoLote = 100;
    public const int MaxCodigosOmitidos = 100;
    public const string MensajeEscenarioFuente = "El escenario solo se admite con la fuente Simulada.";
    public static readonly string MensajeEscenarioValor = $"El escenario debe ser {string.Join(", ", FuenteNovedades.Escenarios)}.";

    /// <summary>Conteos hasta el último lote confirmado (para el log si un lote falla).</summary>
    public ResultadoSincronizacionDto? Parcial { get; private set; }

    public async Task<ResultadoConsulta<ResultadoSincronizacionDto>> SincronizarAsync(string? escenario, CancellationToken ct)
    {
        var esc = LectorParametros.Normalizar(escenario)?.ToLowerInvariant();
        if (esc is not null && (!fuente.AdmiteEscenarios || !FuenteNovedades.Escenarios.Contains(esc)))
        {
            return ResultadoConsulta<ResultadoSincronizacionDto>.Invalido(new Dictionary<string, string[]>
            {
                ["escenario"] = [fuente.AdmiteEscenarios ? MensajeEscenarioValor : MensajeEscenarioFuente],
            });
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;
        ahora = new DateTime(ahora.Ticks - ahora.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

        // 1) Fuente (fuera de la transacción).
        var lectura = await fuente.LeerPermisosMedicosAsync(esc, ct);
        if (lectura.Filas.Count > FuenteNovedades.MaxFilas)
        {
            throw FuenteNovedadesNoDisponibleException.NoDisponible($"La fuente devolvió más de {FuenteNovedades.MaxFilas} filas.");
        }

        var omisiones = new List<OmisionSincronizacion>();
        var mapeados = new List<PermisoMedicoMapeado>();
        foreach (var fila in lectura.Filas)
        {
            var r = MapeoPermisoMedico.Mapear(fila);
            if (r.Mapeado is { } m) mapeados.Add(m); else omisiones.Add(r.Omision!);
        }

        // 2) Lo guardado (fuera de la transacción, en pocas consultas).
        var existentes = (await sincronizacion.ObtenerExistentesPorUidAsync(mapeados.Select(m => m.Uid).Distinct().ToList(), ct))
            .ToDictionary(e => e.Uid);
        var duenosCodigo = await sincronizacion.ObtenerDuenosCodigosAsync(mapeados.Select(m => m.Codigo).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), ct);
        var duenosLegacy = await sincronizacion.ObtenerDuenosLegacyAsync(mapeados.Where(m => m.LegacyId is not null).Select(m => m.LegacyId!.Value).Distinct().ToList(), ct);

        // 3) Empleados: base primero; la API (lectura fresca con cédulas) una sola vez y solo si alguna fila la necesita (A4).
        var resolucion = await ResolverEmpleadosAsync(mapeados, ct);

        // 4) Plan y ausentes.
        var plan = PlanSincronizacion.Armar(mapeados, existentes, duenosCodigo, duenosLegacy, resolucion.EmpleadoDe);
        omisiones.AddRange(plan.Omisiones);
        var presentes = lectura.Filas.Select(f => Guid.TryParse(f.Uid?.Trim(), out var u) ? u : Guid.Empty).ToHashSet();
        var ausentes = (await sincronizacion.ObtenerUidsPermisosMedicosAsync(ct)).Count(u => !presentes.Contains(u));

        // 5) Escritura por lotes de 100 (P3), cada uno en su transacción con applock.
        var total = new ResultadoLote(0, 0, 0, 0, plan.SinCambios);
        ResultadoSincronizacionDto Resultado() => new(ahora, lectura.Fuente, lectura.Filas.Count + lectura.FilasIlegibles,
            total.Nuevas, total.Actualizadas, total.Anuladas, total.Reactivadas, total.SinCambios, ausentes,
            Omitidas(omisiones, lectura.FilasIlegibles),
            omisiones.Select(o => o.Referencia).Take(MaxCodigosOmitidos).ToList());

        Parcial = Resultado();
        foreach (var lote in plan.Escrituras.Chunk(TamanoLote))
        {
            var items = lote.Select(x => x.Item).ToList();
            var altas = items.Where(i => i.EmpleadoId < 0).Select(i => resolucion.Altas[i.EmpleadoId]).DistinctBy(a => a.Id).ToList();
            var cedulas = resolucion.Cedulas.Solo(altas.Select(a => a.CodigoEkon));
            var r = await transaccion.EjecutarAsync(
                ctTx => sincronizacion.AplicarLoteAsync(new LoteSincronizacion(items, altas, cedulas, ahora), ctTx), _ => true, ct);
            total = new ResultadoLote(total.Nuevas + r.Nuevas, total.Actualizadas + r.Actualizadas, total.Anuladas + r.Anuladas,
                total.Reactivadas + r.Reactivadas, total.SinCambios + r.SinCambios);
            Parcial = Resultado();
        }

        return ResultadoConsulta<ResultadoSincronizacionDto>.Ok(Parcial);
    }

    private static IReadOnlyDictionary<string, int> Omitidas(IEnumerable<OmisionSincronizacion> omisiones, int ilegibles)
    {
        var conteos = omisiones.GroupBy(o => o.Categoria).ToDictionary(g => g.Key, g => g.Count());
        if (ilegibles > 0)
        {
            conteos[CategoriaOmision.FilaIlegible] = ilegibles;
        }

        return conteos;
    }

    /// <summary>Empleado de cada fila: Id real, temporal negativo (alta puntual) o null (sinEmpleado).</summary>
    private sealed record Resolucion(
        Func<PermisoMedicoMapeado, int?> EmpleadoDe, IReadOnlyDictionary<int, EmpleadoAsignable> Altas, CedulasEmpleados Cedulas);

    /// <summary>
    /// (1) Fechas.Ekon / Persona.Ekon: fila en Empleado aunque la persona ya no esté activa; sin fila, la API (solo si
    /// está activa: alta puntual). (2) Solo correo (P2): fila de Empleado con ese CorreoEmpresa (coincidencia única);
    /// si no hay, la API (coincidencia única); varias filas → sinEmpleado. (3) Si nada funciona → null.
    /// </summary>
    private async Task<Resolucion> ResolverEmpleadosAsync(IReadOnlyList<PermisoMedicoMapeado> mapeados, CancellationToken ct)
    {
        var codigos = mapeados.Where(m => m.Ekon is not null).Select(m => m.Ekon!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var idPorCodigo = new Dictionary<string, int>(codigos.Count == 0
            ? new Dictionary<string, int>()
            : await datos.ObtenerIdsPorCodigosAsync(codigos, ct), StringComparer.OrdinalIgnoreCase);

        var correos = mapeados.Where(m => m.Ekon is null).Select(m => m.Correo!).Distinct().ToList();
        var porCorreo = correos.Count == 0 ? [] : await sincronizacion.ObtenerEmpleadosPorCorreoAsync(correos, ct);
        var filasPorCorreo = porCorreo.GroupBy(e => e.Correo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Id).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

        var ekonSinFila = codigos.Where(c => !idPorCodigo.ContainsKey(c)).ToList();
        var correosSinFila = correos.Where(c => !filasPorCorreo.ContainsKey(c)).ToList();
        var codigoPorCorreo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var altas = new Dictionary<int, EmpleadoAsignable>();
        var temporalPorCodigo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var cedulas = CedulasEmpleados.Vacio;

        if (ekonSinFila.Count > 0 || correosSinFila.Count > 0)
        {
            var api = await empleadosErp.ObtenerActivosFrescosAsync(ct); // ErpNoDisponibleException → 503 sin escribir
            cedulas = api.Cedulas;
            foreach (var correo in correosSinFila)
            {
                var coincidencias = api.Activos.Where(e => MapeoPermisoMedico.NormalizarCorreo(e.CorreoEmpresa) == correo).ToList();
                if (coincidencias.Count == 1)
                {
                    codigoPorCorreo[correo] = coincidencias[0].CodigoEkon!;
                }
            }

            var codigosApi = ekonSinFila.Where(api.PorCodigo.ContainsKey).Concat(codigoPorCorreo.Values)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var conFila = codigosApi.Count == 0 ? new Dictionary<string, int>() : await datos.ObtenerIdsPorCodigosAsync(codigosApi, ct);
            foreach (var codigo in codigosApi)
            {
                if (conFila.TryGetValue(codigo, out var id))
                {
                    idPorCodigo[codigo] = id;
                    continue;
                }

                var erp = api.PorCodigo[codigo];
                var temporal = -(altas.Count + 1);
                altas[temporal] = new EmpleadoAsignable(temporal, erp.CodigoEkon!, erp.NombreCompleto!, MapeoEmpleado.Normalizar(erp.Puesto), erp);
                temporalPorCodigo[codigo] = temporal;
            }
        }

        int? IdDeCodigo(string codigo) =>
            idPorCodigo.TryGetValue(codigo, out var id) ? id : temporalPorCodigo.TryGetValue(codigo, out var t) ? t : null;

        int? EmpleadoDe(PermisoMedicoMapeado m)
        {
            if (m.Ekon is { } ekon)
            {
                return IdDeCodigo(ekon);
            }

            if (filasPorCorreo.TryGetValue(m.Correo!, out var filas))
            {
                return filas.Count == 1 ? filas[0] : null; // varias filas con el mismo correo: ambiguo
            }

            return codigoPorCorreo.TryGetValue(m.Correo!, out var codigo) ? IdDeCodigo(codigo) : null;
        }

        return new Resolucion(EmpleadoDe, altas, cedulas);
    }
}
