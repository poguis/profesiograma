using System.Globalization;
using App.Application.Comun;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Application.Seguridad;
using App.Domain.Proyectos.Estados;

namespace App.Application.Proyectos.Cabecera;

/// <summary>
/// Caso de uso "Editar cabecera" (TAREA-18): fechas, horario, almuerzo y cambio de actividad de un proyecto ACTIVO.
/// Validar y planificar (EdicionCabeceraValidador) → previsualizar (no guarda) o registrar en una transacción con applock
/// (§7.1), con relectura y recálculo dentro del bloqueo. Sin motor ni cruces: ningún cambio agrega días (C8).
/// Ver docs/fases/FASE_5_Edicion_Cabecera.md.
/// </summary>
public sealed class EdicionCabeceraServicio(
    ICabeceraRepositorio repositorio,
    EdicionCabeceraValidador validador,
    ITransaccionAsignaciones transaccion,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    // ------------------------------------------------------------------ GET cabecera

    public async Task<CabeceraDto?> ObtenerAsync(int proyectoId, CancellationToken ct)
    {
        if (!TryObtenerVisibilidad(out var propietario))
        {
            return null;
        }

        // TAREA-19x: el token se lee ANTES que los datos (si alguien escribe en medio, queda viejo y el registro da 409).
        var version = await repositorio.ObtenerUltimaVersionEtapaAsync(proyectoId, ct);
        var datos = await repositorio.ObtenerAsync(proyectoId, propietario, DateOnly.MaxValue, ct);
        if (datos is null)
        {
            return null;
        }

        var c = datos.Cabecera;
        var hoy = FechaNegocio.Hoy(reloj);
        var motivo = EdicionCabeceraValidador.MotivoNoEditable(c);
        var motivoInicio = EdicionCabeceraValidador.MotivoInicioNoEditable(c, hoy);
        var vigente = ActividadVigente.Elegir(datos.Actividades.Select(a => a.Corte), c.FechaInicio, c.FechaFin, hoy); // O3

        return new CabeceraDto(
            c.Id, c.Codigo, c.EstadoCodigo, motivo is null, motivo, c.GrupoCodigo, c.FechaInicio, c.FechaFin,
            new HorarioCabeceraDto(c.HorarioCodigo, c.HorarioDescripcion, c.HoraEntrada, c.HoraSalida),
            Hora(c.SalidaAlmuerzo), Hora(c.RegresoAlmuerzo),
            vigente is null ? null : Actividad(datos.Actividades.Single(a => a.Id == vigente.Id)),
            datos.Actividades.OrderBy(a => a.Version).Select(Actividad).ToList(),
            new PermisosCabeceraDto(motivo is null && motivoInicio is null, motivoInicio, hoy, motivo is null && c.RequiereProyectoErp),
            new OpcionesAlmuerzoDto(ReglasAlmuerzo.OpcionesSalida, ReglasAlmuerzo.OpcionesRegreso),
            hoy,
            version);
    }

    // ------------------------------------------------------------------ previsualizar / registrar

    public async Task<ResultadoCabecera> PrevisualizarAsync(int proyectoId, EditarCabeceraSolicitud s, CancellationToken ct)
    {
        var version = await repositorio.ObtenerUltimaVersionEtapaAsync(proyectoId, ct); // TAREA-19x: antes que los datos
        var r = await CalcularAsync(proyectoId, s, version, ct);
        return r.Error ?? ResultadoCabecera.Previsualizado(r.Calculo!.Previsualizacion);
    }

    public async Task<ResultadoCabecera> RegistrarAsync(int proyectoId, EditarCabeceraSolicitud s, CancellationToken ct)
    {
        // TAREA-19x (P3): sin token → 400, sin leer la base ni abrir la transacción.
        ArgumentNullException.ThrowIfNull(s);
        if (s.VersionProyecto is not int token)
        {
            return ResultadoCabecera.Invalido(VersionProyecto.ErroresFalta());
        }

        var previo = await CalcularAsync(proyectoId, s, token, ct);
        if (previo.Error is { Estado: EstadoEdicion.NoEncontrado } noEncontrado)
        {
            return noEncontrado;
        }

        // TAREA-19x (P1): token viejo → 409 antes de cualquier 400 o "No hay cambios" (V15 de la TAREA-19a).
        if (await repositorio.ObtenerUltimaVersionEtapaAsync(proyectoId, ct) != token)
        {
            return ResultadoCabecera.Cambiado();
        }

        if (previo.Error is { } error)
        {
            return error;
        }

        if (previo.Calculo!.Plan.SinCambios)
        {
            return ResultadoCabecera.Invalido(ResultadoEdicionPersonal.ErroresSinCambios()); // C9
        }

        return await transaccion.EjecutarAsync(async ctTx =>
        {
            // C8: dentro del applock se vuelve a leer y a calcular. Si ya no es válido, o cambiaron el estado, las fechas
            // o el RowVer del proyecto desde la lectura de fuera, el proyecto cambió (409).
            var r = await CalcularAsync(proyectoId, s, token, ctTx);
            if (r.Error is { } errorTx)
            {
                return errorTx.Estado == EstadoEdicion.Invalido ? ResultadoCabecera.Cambiado() : errorTx;
            }

            var antes = previo.Calculo.Datos.Cabecera;
            var ahora = r.Calculo!.Datos.Cabecera;
            if (!string.Equals(antes.EstadoCodigo, ahora.EstadoCodigo, StringComparison.OrdinalIgnoreCase)
                || antes.FechaInicio != ahora.FechaInicio || antes.FechaFin != ahora.FechaFin
                || !antes.RowVer.AsSpan().SequenceEqual(ahora.RowVer) || r.Calculo.Plan.SinCambios)
            {
                return ResultadoCabecera.Cambiado();
            }

            // TAREA-19x: comprobación definitiva del token dentro del applock, antes de calcular la versión nueva.
            var actual = await repositorio.ObtenerUltimaVersionEtapaAsync(proyectoId, ctTx);
            if (actual != token)
            {
                return ResultadoCabecera.Cambiado();
            }

            try
            {
                var version = actual + 1;
                await repositorio.AplicarAsync(CrearCambio(r.Calculo, version), ctTx);
                return ResultadoCabecera.Hecho(new CabeceraActualizadaDto(proyectoId, version));
            }
            catch (ConflictoConcurrenciaException)
            {
                return ResultadoCabecera.Cambiado(); // se revierte (confirmar = false)
            }
        }, x => x.Estado == EstadoEdicion.Realizado, ct);
    }

    // ------------------------------------------------------------------ cálculo

    private sealed record Calculo(DatosCabecera Datos, DateOnly Hoy, PlanCabecera Plan, PrevisualizacionCabeceraDto Previsualizacion);

    private sealed record Resultado(ResultadoCabecera? Error, Calculo? Calculo);

    /// <param name="versionProyecto">Token que lleva la vista previa (leído antes que los datos, o el del registro).</param>
    private async Task<Resultado> CalcularAsync(int proyectoId, EditarCabeceraSolicitud s, int versionProyecto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!TryObtenerVisibilidad(out var propietario))
        {
            return new Resultado(ResultadoCabecera.NoEncontrado(), null);
        }

        // Días con Fecha > nueva fecha fin: solo hacen falta al acortar (C4). Sin fecha fin no se lee ninguno.
        var datos = await repositorio.ObtenerAsync(proyectoId, propietario, s.FechaFin ?? DateOnly.MaxValue, ct);
        if (datos is null)
        {
            return new Resultado(ResultadoCabecera.NoEncontrado(), null);
        }

        var hoy = FechaNegocio.Hoy(reloj);
        var validacion = await validador.ValidarAsync(s, datos, hoy, ct);
        if (validacion.Plan is not { } plan)
        {
            return new Resultado(ResultadoCabecera.Invalido(validacion.Errores), null);
        }

        return new Resultado(null, new Calculo(datos, hoy, plan, CrearPrevisualizacion(datos, hoy, plan, versionProyecto)));
    }

    private static PrevisualizacionCabeceraDto CrearPrevisualizacion(DatosCabecera datos, DateOnly hoy, PlanCabecera plan, int versionProyecto)
    {
        var c = datos.Cabecera;
        var cambios = new List<CambioCampoDto>();
        if (plan.CambiaInicio)
        {
            cambios.Add(new CambioCampoDto("fechaInicio", Fecha(c.FechaInicio), Fecha(plan.FechaInicio)));
        }

        if (plan.CambiaFin)
        {
            cambios.Add(new CambioCampoDto("fechaFin", Fecha(c.FechaFin), Fecha(plan.FechaFin)));
        }

        if (plan.HorarioNuevo is { } horario)
        {
            cambios.Add(new CambioCampoDto("horario", c.HorarioCodigo is null ? null : $"{c.HorarioCodigo} – {c.HorarioDescripcion}",
                $"{horario.Codigo} – {horario.Descripcion}"));
        }

        if (plan.SalidaAlmuerzo != c.SalidaAlmuerzo)
        {
            cambios.Add(new CambioCampoDto("salidaAlmuerzo", Hora(c.SalidaAlmuerzo), Hora(plan.SalidaAlmuerzo)));
        }

        if (plan.RegresoAlmuerzo != c.RegresoAlmuerzo)
        {
            cambios.Add(new CambioCampoDto("regresoAlmuerzo", Hora(c.RegresoAlmuerzo), Hora(plan.RegresoAlmuerzo)));
        }

        if (plan.ActividadNueva is { } nueva)
        {
            var anterior = ActividadVigente.Elegir(datos.Actividades.Select(a => a.Corte), nueva.FechaInicio); // la vigente en "desde"
            cambios.Add(new CambioCampoDto("actividad", anterior?.Codigo, $"{nueva.Codigo} desde {Fecha(nueva.FechaInicio)}"));
        }

        var advertencias = new List<string>();
        ImpactoRecorte? impacto = null;
        if (plan.Recorte is { } recorte)
        {
            impacto = VistaRecorte.Crear(datos.Personal, datos.Actividades.Select(a => a.Corte).ToList(), recorte);
            advertencias.AddRange(impacto.AdvertenciasBacks);
        }

        advertencias.AddRange(plan.Advertencias);

        return new PrevisualizacionCabeceraDto(
            hoy, plan.TipoEtapa, plan.FechaInicio, plan.FechaFin, cambios,
            impacto?.DiasEliminados ?? [], impacto?.PersonalEliminado ?? [], impacto?.PersonalRecortado ?? [],
            DiasAgregados(datos, plan), ActividadesResultantes(datos, plan), advertencias, versionProyecto);
    }

    /// <summary>H15: días agregados por persona (desde / hasta / cantidad).</summary>
    private static List<DiasAgregadosDto> DiasAgregados(DatosCabecera datos, PlanCabecera plan)
    {
        var personas = datos.Personal.ToDictionary(p => p.Corte.Id);
        return plan.DescansosAgregados
            .GroupBy(d => d.PersonalId)
            .Select(g => new DiasAgregadosDto(VistaRecorte.Empleado(personas[g.Key]), "DESCANSO", g.Count(),
                g.Min(d => d.Dia.Fecha), g.Max(d => d.Dia.Fecha)))
            .ToList();
    }

    private static List<ActividadResultanteDto> ActividadesResultantes(DatosCabecera datos, PlanCabecera plan)
    {
        var finales = plan.ActividadesFinales.ToDictionary(a => a.Id);
        var lista = datos.Actividades.Select(a => finales.TryGetValue(a.Id, out var f)
                ? f.FechaInicio == a.FechaInicio && f.FechaFin == a.FechaFin
                    ? new ActividadResultanteDto(a.Version, a.Codigo, a.Descripcion, a.FechaInicio, a.FechaFin, null, null, "SIN_CAMBIO")
                    : new ActividadResultanteDto(a.Version, a.Codigo, a.Descripcion, f.FechaInicio, f.FechaFin, a.FechaInicio, a.FechaFin, "MODIFICADA")
                : new ActividadResultanteDto(a.Version, a.Codigo, a.Descripcion, a.FechaInicio, a.FechaFin, null, null, "ELIMINADA"))
            .ToList();

        if (plan.ActividadNueva is { } n)
        {
            lista.Add(new ActividadResultanteDto(n.Version, n.Codigo, n.Descripcion, n.FechaInicio, n.FechaFin, null, null, "NUEVA"));
        }

        return lista;
    }

    // ------------------------------------------------------------------ escritura

    private static CambioCabeceraAplicar CrearCambio(Calculo c, int version)
    {
        var plan = c.Plan;
        var originales = c.Datos.Actividades.ToDictionary(a => a.Id);
        var modificadas = plan.ActividadesFinales
            .Where(f => originales[f.Id].FechaInicio != f.FechaInicio || originales[f.Id].FechaFin != f.FechaFin)
            .Select(f => new ActividadModificada(f.Id, f.FechaInicio, f.FechaFin))
            .ToList();

        // O2: la etapa CAMBIO_ACTIVIDAD lleva la actividad nueva. Las demás (EDICION_CABECERA), la regla común de etapas
        // (O3): vigente en max(inicio, min(FechaCorte = hoy, fin)) con las fechas resultantes, contando la nueva.
        string? actividadEtapa;
        if (plan.TipoEtapa == EdicionCabeceraValidador.TipoCambioActividad)
        {
            actividadEtapa = plan.ActividadNueva!.Codigo;
        }
        else
        {
            var finales = plan.ActividadesFinales.ToList();
            if (plan.ActividadNueva is { } n)
            {
                finales.Add(new ActividadCorte(0, n.Version, n.Codigo, n.FechaInicio, n.FechaFin));
            }

            actividadEtapa = ActividadVigente.Elegir(finales, plan.FechaInicio, plan.FechaFin, c.Hoy)?.Codigo;
        }

        var horario = plan.HorarioNuevo is { } h
            ? new HorarioNuevo(h.Codigo, Truncar(h.Descripcion, 200), h.HoraEntrada, h.HoraSalida, h.MinutosJornada, h.MinutosTrabajados,
                Truncar(h.Tipo, 5))
            : null;

        // C7: estado sin cambios; fechas resultantes; corte = hoy; snapshot del personal resultante.
        return new CambioCabeceraAplicar(c.Datos.Cabecera.Id, version, plan.TipoEtapa, plan.FechaInicio, plan.FechaFin, c.Hoy,
            actividadEtapa, VistaRecorte.Snapshot(c.Datos.Personal, plan.Recorte), horario, plan.SalidaAlmuerzo, plan.RegresoAlmuerzo,
            plan.Recorte, plan.DescansosAgregados, plan.ActividadesEliminadas, modificadas, plan.ActividadNueva);
    }

    // ------------------------------------------------------------------ utilidades

    private static ActividadCabeceraDto Actividad(ActividadGuardada a) =>
        new(a.Version, a.Codigo, a.Descripcion, a.Tipo, a.TipoMovimiento, a.FechaInicio, a.FechaFin);

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Hora(TimeOnly? hora) => hora is TimeOnly h ? ReglasAlmuerzo.Formato(h) : null;

    private static string? Truncar(string? valor, int maximo) =>
        valor is null || valor.Length <= maximo ? valor : valor[..maximo];

    /// <summary>R1: Admin ve todos (null); cualquier otro rol solo sus proyectos.</summary>
    private bool TryObtenerVisibilidad(out int? propietarioUsuarioId)
    {
        if (usuario.TieneRol(RolesApp.Admin))
        {
            propietarioUsuarioId = null;
            return true;
        }

        propietarioUsuarioId = usuario.UsuarioId;
        return propietarioUsuarioId is not null;
    }
}
