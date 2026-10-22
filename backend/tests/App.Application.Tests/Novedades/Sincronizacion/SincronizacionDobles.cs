using App.Application.Novedades.Sincronizacion;

namespace App.Application.Tests.Novedades.Sincronizacion;

/// <summary>DETALLE_JSON y filas FICTICIAS con la forma del original (A6: nunca datos reales de la lista).</summary>
internal static class JsonFicticio
{
    public static Guid Uid(int n) => Guid.Parse($"0f1c7a00-0000-4000-8000-{n:000000000000}");

    public static string Json(int n, string[]? dias = null, string? fechasEkon = null, string? personaEkon = null, string? correo = null,
        string tipo = "PERMISO MEDICO", string origenPermiso = "aplicacionPermisosMedicos", string aplicacion = "PERSONA",
        string? inicio = null, string? fin = null, string? fechaRegistro = "2070-02-01 10:00:00", string? origen = "PLANTA")
    {
        static string T(string? v) => v is null ? "null" : $"\"{v}\"";
        inicio ??= dias?[0];
        fin ??= dias?[^1];
        var diasJson = dias is null ? "" : $",\"Dias\":[{string.Join(",", dias.Select(d => $"{{\"Fecha\":\"{d}\",\"FechaClave\":\"{d}\"}}"))}]";
        var pEkon = personaEkon is null ? "" : $",\"Ekon\":\"{personaEkon}\"";
        var fEkon = fechasEkon is null ? "" : $",\"Ekon\":\"{fechasEkon}\"";
        return $"{{\"NovedadUID\":\"{Uid(n)}\",\"TipoAplicacion\":\"{aplicacion}\",\"TipoNovedad\":\"{tipo}\"," +
               $"\"Persona\":{{\"Correo\":{T(correo)},\"Nombre\":\"PERSONA FICTICIA {n:00}\"{pEkon}}}," +
               $"\"OrigenPermiso\":\"{origenPermiso}\",\"Origen\":{T(origen)}," +
               $"\"Fechas\":{{\"FechaInicio\":{T(inicio)},\"FechaFin\":{T(fin)}{fEkon}}}{diasJson}," +
               $"\"Registro\":{{\"FechaRegistro\":{T(fechaRegistro)},\"UsuarioRegistro\":\"REGISTRANTE FICTICIO\",\"CorreoRegistro\":\"registro.ficticio@profesiograma.local\"}}}}";
    }

    public static FilaNovedadExterna Fila(int n, string? json, string estado = "ACTIVA", string? codigo = null) =>
        new(1_000 + n, codigo ?? $"NOV-FICT-{n:0000}", Uid(n).ToString(), estado, json, null);

    /// <summary>Fila válida de PERMISO MEDICO de un empleado DEV con fila (DatosFalsos: DEV001 = Id 1…).</summary>
    public static FilaNovedadExterna Valida(int n, string ekon = "DEV001", string estado = "ACTIVA") =>
        Fila(n, Json(n, dias: [$"2070-03-{n % 28 + 1:00}"], fechasEkon: ekon), estado);
}

internal sealed class FuenteFalsa(params FilaNovedadExterna[] filas) : IFuenteNovedadesExternas
{
    public List<string> Eventos { get; set; } = [];
    public Exception? Error { get; set; }
    public int FilasIlegibles { get; set; }
    public bool Escenarios { get; set; } = true;
    public string? EscenarioRecibido { get; private set; }

    public string Fuente => "Falsa";
    public bool AdmiteEscenarios => Escenarios;

    public Task<LecturaNovedadesExternas> LeerPermisosMedicosAsync(string? escenario, CancellationToken ct)
    {
        Eventos.Add("fuente");
        EscenarioRecibido = escenario;
        return Error is null
            ? Task.FromResult(new LecturaNovedadesExternas(Fuente, filas, FilasIlegibles))
            : Task.FromException<LecturaNovedadesExternas>(Error);
    }
}

/// <summary>
/// Base falsa de la sincronización. AplicarLote reclasifica con <see cref="EnBase"/> (lo que «hay» dentro de la
/// transacción) usando PlanSincronizacion.Clasificar, como el repositorio real (A1), y lo actualiza.
/// </summary>
internal sealed class SincronizacionFalsa(List<string> eventos) : ISincronizacionNovedades
{
    public Dictionary<Guid, NovedadExistente> Existentes { get; } = [];
    public Dictionary<Guid, NovedadExistente> EnBase { get; } = [];
    public Dictionary<string, Guid> DuenosCodigo { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, Guid> DuenosLegacy { get; } = [];
    public List<EmpleadoPorCorreo> PorCorreo { get; } = [];
    public List<Guid> UidsPermisosMedicos { get; } = [];
    public List<LoteSincronizacion> Lotes { get; } = [];

    public Task<IReadOnlyList<NovedadExistente>> ObtenerExistentesPorUidAsync(IReadOnlyCollection<Guid> uids, CancellationToken ct)
    {
        eventos.Add("existentes");
        return Task.FromResult<IReadOnlyList<NovedadExistente>>(Existentes.Values.Where(e => uids.Contains(e.Uid)).ToList());
    }

    public Task<IReadOnlyDictionary<string, Guid>> ObtenerDuenosCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, Guid>>(DuenosCodigo.Where(d => codigos.Contains(d.Key)).ToDictionary());

    public Task<IReadOnlyDictionary<int, Guid>> ObtenerDuenosLegacyAsync(IReadOnlyCollection<int> legacyIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<int, Guid>>(DuenosLegacy.Where(d => legacyIds.Contains(d.Key)).ToDictionary());

    public Task<IReadOnlyList<EmpleadoPorCorreo>> ObtenerEmpleadosPorCorreoAsync(IReadOnlyCollection<string> correos, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EmpleadoPorCorreo>>(PorCorreo.Where(e => correos.Contains(e.Correo, StringComparer.OrdinalIgnoreCase)).ToList());

    public Task<IReadOnlyList<Guid>> ObtenerUidsPermisosMedicosAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(UidsPermisosMedicos);

    public Task<ResultadoLote> AplicarLoteAsync(LoteSincronizacion lote, CancellationToken ct)
    {
        eventos.Add("lote");
        Lotes.Add(lote);
        int nuevas = 0, actualizadas = 0, anuladas = 0, reactivadas = 0, sinCambios = 0;
        foreach (var item in lote.Items)
        {
            EnBase.TryGetValue(item.Mapeado.Uid, out var e);
            switch (PlanSincronizacion.Clasificar(item.Mapeado, item.EmpleadoId, e))
            {
                case AccionSincronizacion.Nueva: nuevas++; break;
                case AccionSincronizacion.Actualizar: actualizadas++; break;
                case AccionSincronizacion.Anular: anuladas++; break;
                case AccionSincronizacion.Reactivar: reactivadas++; break;
                default: sinCambios++; break;
            }
        }

        return Task.FromResult(new ResultadoLote(nuevas, actualizadas, anuladas, reactivadas, sinCambios));
    }
}
