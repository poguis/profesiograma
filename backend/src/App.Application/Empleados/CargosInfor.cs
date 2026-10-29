using System.Globalization;
using System.Text.RegularExpressions;
using App.Application.Comun;
using App.Application.Erp;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Seguridad;

namespace App.Application.Empleados;

// TAREA-25-3-1: administración de CargoInfor (puesto → código Infor del reporte, D14). Solo Admin. Sin alta manual (F1):
// los cargos vienen de la API (importación) y de la alta puntual. Cargo no se edita (es la clave contra Empleado.Puesto) y
// nunca se borra. Ver docs/fases/FASE_5_Usuarios_Departamentos.md §10.

public enum FiltroCodigoInfor { Todos, SinCodigo, ConCodigo }

/// <summary>GET /api/admin/cargos-infor (query string como texto).</summary>
public sealed record CargoInforListadoSolicitud(string? Texto, string? Filtro, string? IncluirInactivos, string? Pagina, string? Tamano);

public sealed record CargoInforFiltro(string? Texto, FiltroCodigoInfor Filtro, bool IncluirInactivos, int Pagina, int Tamano);

/// <summary>Fila de la base: PersonasGuardadas = filas de Empleado con ese puesto (misma collation que el reporte).</summary>
public sealed record CargoInforFila(int Id, string Cargo, string? CodigoInfor, bool Activo, int PersonasGuardadas);

/// <summary>ActivosErp = activos de la lista en caché con ese puesto; null si no hay lista utilizable (nunca 503).</summary>
public sealed record CargoInforDto(int Id, string Cargo, string? CodigoInfor, bool Activo, int PersonasGuardadas, int? ActivosErp);

public sealed record CargoRegistro(int Id, string Cargo, string? CodigoInfor, bool Activo);

/// <param name="Variantes">Hasta 20 grupos de cargos que solo difieren en tildes o espacios internos (cada uno es una fila).</param>
public sealed record VistaPreviaImportacionDto(
    int PuestosApi, int Nuevos, int YaExistentes, int ExistentesInactivos, int Fusionados, int GruposVariantes,
    IReadOnlyList<IReadOnlyList<string>> Variantes, DateTimeOffset ObtenidaUtc, bool EsAnterior);

public sealed record ImportacionCargosDto(int PuestosApi, int Insertados, int YaExistentes, int ExistentesInactivos);

/// <summary>PUT: CodigoInforAnterior y ActivoAnterior = los valores que vio el Admin (CodigoInforAnterior null = sin código).</summary>
public sealed record EditarCargoInforSolicitud(string? CodigoInfor, bool? Activo, string? CodigoInforAnterior, bool? ActivoAnterior);

public sealed record CargoInforEditadoDto(int Id, string? CodigoInfor, bool Activo);

public interface ICargosInforRepositorio
{
    Task<PaginaResultado<CargoInforFila>> ListarAsync(CargoInforFiltro filtro, CancellationToken ct);

    /// <summary>Toda la tabla (es chica). Dentro de ITransaccionAsignaciones, relectura antes de insertar.</summary>
    Task<IReadOnlyList<CargoRegistro>> ObtenerTodosAsync(CancellationToken ct);

    /// <summary>
    /// Inserta los cargos (CodigoInfor NULL, Activo 1; auditoría por el AuditoriaInterceptor).
    /// </summary>
    /// <exception cref="ConflictoConcurrenciaException">UQ_CargoInfor_Cargo (otra alta o puestos que la base considera iguales).</exception>
    Task InsertarAsync(IReadOnlyList<string> cargos, CancellationToken ct);

    /// <summary>
    /// UPDATE … WHERE Id AND CodigoInfor = anterior (NULL = IS NULL) AND Activo = activoAnterior (ExecuteUpdate,
    /// compare-and-set), con ModificadoPorId y FechaModificacion explícitos. Devuelve las filas afectadas.
    /// </summary>
    Task<int> ActualizarAsync(int id, string? codigoAnterior, bool activoAnterior, string? codigo, bool activo, int usuarioId,
        DateTime fechaUtc, CancellationToken ct);

    Task<bool> ExisteAsync(int id, CancellationToken ct);
}

/// <summary>Plan de la importación (lógica pura). Nuevos: los cargos que hay que insertar (CodigoInfor NULL).</summary>
public sealed record PlanImportacionCargos(
    int PuestosApi, IReadOnlyList<string> Nuevos, int YaExistentes, int ExistentesInactivos, int Fusionados,
    IReadOnlyList<IReadOnlyList<string>> Variantes);

/// <summary>Reglas puras de CargoInfor: comparador de la collation, plan de importación y código Infor.</summary>
public static class ReglasCargoInfor
{
    public const int LargoCodigo = 20; // CargoInfor.CodigoInfor VARCHAR(20)
    public const int MaximoVariantes = 20;
    public const string MensajeCodigoLargo = "El código Infor supera los 20 caracteres.";
    public const string MensajeCodigoFormato = "El código Infor solo admite letras, números, punto, guion y guion bajo.";

    /// <summary>
    /// Imita Modern_Spanish_CI_AS (UQ_CargoInfor_Cargo y el join del reporte): sin distinguir mayúsculas, kana ni ancho;
    /// SÍ distingue tildes. Los espacios internos cuentan (el trim lo hace NormalizarCargo).
    /// </summary>
    public static readonly StringComparer Collation = StringComparer.Create(CultureInfo.GetCultureInfo("es-EC"),
        CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth);

    private static readonly Regex FormatoCodigo = new("^[A-Z0-9._-]+$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Cargos de los activos con <see cref="MapeoEmpleado.NormalizarCargo"/> (la misma regla de la alta puntual) y
    /// deduplicados con <see cref="Collation"/>. Fusionados = textos de la API (sin espacios extremos) que terminan en un
    /// cargo ya contado. Variantes = grupos de cargos distintos que solo difieren en tildes o espacios internos
    /// (<see cref="ComparacionEmpleadosErp.Iguales"/>), entre los de la API y los guardados, con al menos uno de la API.
    /// </summary>
    public static PlanImportacionCargos Planificar(IEnumerable<EmpleadoErp> activos, IEnumerable<CargoRegistro> existentes)
    {
        ArgumentNullException.ThrowIfNull(activos);
        ArgumentNullException.ThrowIfNull(existentes);
        var textos = activos.Select(e => MapeoEmpleado.Normalizar(e.Puesto)).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var cargos = new List<string>();
        var vistos = new HashSet<string>(Collation);
        foreach (var cargo in textos.Select(MapeoEmpleado.NormalizarCargo).OfType<string>())
        {
            if (vistos.Add(cargo))
            {
                cargos.Add(cargo);
            }
        }

        var guardados = new Dictionary<string, CargoRegistro>(Collation);
        foreach (var c in existentes)
        {
            guardados.TryAdd(c.Cargo, c);
        }

        int yaExistentes = 0, inactivos = 0;
        var nuevos = new List<string>();
        foreach (var cargo in cargos)
        {
            if (!guardados.TryGetValue(cargo, out var guardado))
            {
                nuevos.Add(cargo);
            }
            else if (guardado.Activo)
            {
                yaExistentes++;
            }
            else
            {
                inactivos++;
            }
        }

        return new PlanImportacionCargos(cargos.Count, nuevos, yaExistentes, inactivos, textos.Count - cargos.Count,
            Variantes(cargos, guardados.Keys));
    }

    private static List<IReadOnlyList<string>> Variantes(IReadOnlyList<string> api, IEnumerable<string> guardados)
    {
        var grupos = new List<(List<string> Cargos, bool ConApi)>();
        var todos = api.Select(c => (Cargo: c, Api: true))
            .Concat(guardados.Where(g => !api.Contains(g, Collation)).Select(g => (Cargo: g, Api: false)));
        foreach (var (cargo, deApi) in todos)
        {
            var i = grupos.FindIndex(g => ComparacionEmpleadosErp.Iguales(g.Cargos[0], cargo));
            if (i < 0)
            {
                grupos.Add(([cargo], deApi));
            }
            else
            {
                grupos[i].Cargos.Add(cargo);
                grupos[i] = (grupos[i].Cargos, grupos[i].ConApi || deApi);
            }
        }

        return grupos.Where(g => g.ConApi && g.Cargos.Count > 1)
            .Select(g => (IReadOnlyList<string>)g.Cargos.Order(StringComparer.Ordinal).ToList())
            .OrderBy(g => g[0], StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>F2: recortado y en mayúsculas; vacío = null (quitar el código).</summary>
    public static string? NormalizarCodigo(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim().ToUpperInvariant();

    /// <summary>Mensaje de error del código ya normalizado (null = válido o sin código).</summary>
    public static string? ValidarCodigo(string? codigo) =>
        codigo is null ? null
        : codigo.Length > LargoCodigo ? MensajeCodigoLargo
        : FormatoCodigo.IsMatch(codigo) ? null
        : MensajeCodigoFormato;
}

public enum EstadoCargoInfor { Ok, Invalido, NoEncontrado, Conflicto }

public sealed record ResultadoCargoInfor<T>(EstadoCargoInfor Estado, T? Valor = default, IReadOnlyDictionary<string, string[]>? Errores = null);

public sealed class CargosInforServicio(
    ICargosInforRepositorio repositorio, IFuenteEmpleadosErp fuente, ITransaccionAsignaciones transaccion, IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const int TamanoPorDefecto = 25, TamanoMaximo = 100;
    public const string MensajeFiltro = "El filtro debe ser todos, sinCodigo o conCodigo.";
    public const string MensajeActivoFalta = "Indique si el cargo está activo.";
    public const string MensajeAnteriores = "Faltan los valores anteriores del cargo; recargue.";
    public const string MensajeNoExiste = "El cargo no existe.";
    public const string MensajeConflicto = "Otro usuario cambió el cargo; recargue.";
    public const string MensajeConflictoImportacion =
        "No se pudo importar: otro registro se guardó al mismo tiempo o hay puestos que la base considera iguales. Vuelva a intentarlo; si se repite, avise a TI.";

    /// <summary>K9: activosErp desde la caché; si no hay lista utilizable, null (la pantalla no depende del ERP).</summary>
    public async Task<ResultadoConsulta<PaginaResultado<CargoInforDto>>> ListarAsync(CargoInforListadoSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var errores = new Dictionary<string, string[]>();
        var filtro = LeerFiltro(s.Filtro, errores);
        var incluirInactivos = LectorParametros.LeerBooleano(s.IncluirInactivos, "incluirInactivos", false, errores);
        var pagina = LectorParametros.LeerPagina(s.Pagina, errores);
        var tamano = LectorParametros.LeerTamano(s.Tamano, TamanoPorDefecto, TamanoMaximo, errores);
        if (errores.Count > 0)
        {
            return ResultadoConsulta<PaginaResultado<CargoInforDto>>.Invalido(errores);
        }

        var pag = await repositorio.ListarAsync(
            new CargoInforFiltro(LectorParametros.Normalizar(s.Texto), filtro, incluirInactivos, pagina, tamano), ct);
        Dictionary<string, int>? porCargo = null;
        try
        {
            porCargo = (await fuente.ObtenerActivosAsync(ct)).Activos
                .Select(e => MapeoEmpleado.NormalizarCargo(e.Puesto)).OfType<string>()
                .GroupBy(c => c, ReglasCargoInfor.Collation)
                .ToDictionary(g => g.Key, g => g.Count(), ReglasCargoInfor.Collation);
        }
        catch (ErpNoDisponibleException)
        {
            // Sin lista utilizable: activosErp = null.
        }

        var items = pag.Items
            .Select(c => new CargoInforDto(c.Id, c.Cargo, c.CodigoInfor, c.Activo, c.PersonasGuardadas,
                porCargo is null ? null : porCargo.GetValueOrDefault(c.Cargo)))
            .ToList();
        return ResultadoConsulta<PaginaResultado<CargoInforDto>>.Ok(new PaginaResultado<CargoInforDto>(items, pag.Pagina, pag.Tamano, pag.Total));
    }

    /// <summary>K4: vista previa con la lista en caché (F4); sin lista utilizable → ErpNoDisponibleException (503). No escribe.</summary>
    public async Task<VistaPreviaImportacionDto> VistaPreviaAsync(CancellationToken ct)
    {
        var lista = await fuente.ObtenerActivosAsync(ct);
        var plan = ReglasCargoInfor.Planificar(lista.Activos, await repositorio.ObtenerTodosAsync(ct));
        return new VistaPreviaImportacionDto(plan.PuestosApi, plan.Nuevos.Count, plan.YaExistentes, plan.ExistentesInactivos,
            plan.Fusionados, plan.Variantes.Count, plan.Variantes.Take(ReglasCargoInfor.MaximoVariantes).ToList(), lista.ObtenidaUtc,
            lista.EsAnterior);
    }

    /// <summary>
    /// K1/K5: lectura FRESCA fuera de la transacción (ERP caído → 503 sin abrirla); dentro de ITransaccionAsignaciones (el
    /// applock de la alta puntual) relee la tabla e inserta solo los faltantes. Idempotente; no reactiva inactivos (K6).
    /// </summary>
    public async Task<ResultadoCargoInfor<ImportacionCargosDto>> ImportarAsync(CancellationToken ct)
    {
        var lista = await fuente.ObtenerActivosFrescosAsync(ct);
        try
        {
            var dto = await transaccion.EjecutarAsync(async ctTx =>
            {
                var plan = ReglasCargoInfor.Planificar(lista.Activos, await repositorio.ObtenerTodosAsync(ctTx));
                if (plan.Nuevos.Count > 0)
                {
                    await repositorio.InsertarAsync(plan.Nuevos, ctTx);
                }

                return new ImportacionCargosDto(plan.PuestosApi, plan.Nuevos.Count, plan.YaExistentes, plan.ExistentesInactivos);
            }, _ => true, ct);
            return new ResultadoCargoInfor<ImportacionCargosDto>(EstadoCargoInfor.Ok, dto);
        }
        catch (ConflictoConcurrenciaException)
        {
            return new ResultadoCargoInfor<ImportacionCargosDto>(EstadoCargoInfor.Conflicto);
        }
    }

    /// <summary>K8: compare-and-set por valores; 0 filas → 404 si el Id no existe, si no 409.</summary>
    public async Task<ResultadoCargoInfor<CargoInforEditadoDto>> EditarAsync(int id, EditarCargoInforSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var errores = new Dictionary<string, string[]>();
        var codigo = ReglasCargoInfor.NormalizarCodigo(s.CodigoInfor);
        if (ReglasCargoInfor.ValidarCodigo(codigo) is { } mensaje)
        {
            errores["codigoInfor"] = [mensaje];
        }

        if (s.Activo is null)
        {
            errores["activo"] = [MensajeActivoFalta];
        }

        if (s.ActivoAnterior is null)
        {
            errores["anterior"] = [MensajeAnteriores];
        }

        if (errores.Count > 0)
        {
            return new ResultadoCargoInfor<CargoInforEditadoDto>(EstadoCargoInfor.Invalido, Errores: errores);
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var fecha = new DateTime(ahora.Ticks - (ahora.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc); // DATETIME2(0)
        var filas = await repositorio.ActualizarAsync(id, ReglasCargoInfor.NormalizarCodigo(s.CodigoInforAnterior), s.ActivoAnterior!.Value,
            codigo, s.Activo!.Value, usuario.UsuarioId ?? Domain.Seguridad.Usuario.IdSistema, fecha, ct);
        if (filas > 0)
        {
            return new ResultadoCargoInfor<CargoInforEditadoDto>(EstadoCargoInfor.Ok, new CargoInforEditadoDto(id, codigo, s.Activo.Value));
        }

        return new ResultadoCargoInfor<CargoInforEditadoDto>(
            await repositorio.ExisteAsync(id, ct) ? EstadoCargoInfor.Conflicto : EstadoCargoInfor.NoEncontrado);
    }

    private static FiltroCodigoInfor LeerFiltro(string? valor, Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return FiltroCodigoInfor.Todos;
        }

        switch (valor.Trim().ToLowerInvariant())
        {
            case "todos":
                return FiltroCodigoInfor.Todos;
            case "sincodigo":
                return FiltroCodigoInfor.SinCodigo;
            case "concodigo":
                return FiltroCodigoInfor.ConCodigo;
            default:
                errores["filtro"] = [MensajeFiltro];
                return FiltroCodigoInfor.Todos;
        }
    }
}
