using App.Application.Proyectos.Estados;
using App.Application.Seguridad;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;

namespace App.Application.Tests.Proyectos.Estados;

/// <summary>
/// Proyecto de prueba (como el Id 7 de desarrollo): 01/03–30/04/2027.
/// P1 (Id 70) DEV004 TIPO_3 todo el rango; Back (Id 71) DEV008 10–12/03 relacionado con P1;
/// P2 (Id 72) DEV005 desde el 20/03; Back (Id 73) DEV006 10–12/03 relacionado con P2. Actividad DEV.01 v1 todo el rango.
/// </summary>
internal static class DoblesEstados
{
    public const int ProyectoId = 7;
    public static readonly DateOnly Inicio = new(2027, 3, 1);
    public static readonly DateOnly Fin = new(2027, 4, 30);

    public static DatosCambioEstado Proyecto(string estado = "ACTIVO", DateOnly? fin = null)
    {
        var finProyecto = fin ?? Fin;
        PersonalCambio[] personal =
        [
            new(new PersonaCorte(70, RolCronograma.Principal, 1, 4, Inicio, finProyecto, null), "DEV004", "EMPLEADO PRUEBA 04", "TIPO_3", 5, 2, "JORNADA"),
            new(new PersonaCorte(72, RolCronograma.Principal, 2, 5, new DateOnly(2027, 3, 20), finProyecto, null), "DEV005", "EMPLEADO PRUEBA 05", "TIPO_3", 5, 2, "JORNADA"),
            new(new PersonaCorte(71, RolCronograma.Back, 1, 8, new DateOnly(2027, 3, 10), new DateOnly(2027, 3, 12), 70), "DEV008", "EMPLEADO PRUEBA 08", null, null, 2, "JORNADA"),
            new(new PersonaCorte(73, RolCronograma.Back, 2, 6, new DateOnly(2027, 3, 10), new DateOnly(2027, 3, 12), 72), "DEV006", "EMPLEADO PRUEBA 06", null, null, 0, "JORNADA"),
        ];

        // Días posteriores al 15/03 (los que el repositorio real cargaría para F = 15/03).
        var dias = new List<DiaCorte>();
        for (var d = new DateOnly(2027, 3, 16); d <= finProyecto; d = d.AddDays(1))
        {
            dias.Add(new DiaCorte(70, d, RolCronograma.Principal));
            if (d >= new DateOnly(2027, 3, 20))
            {
                dias.Add(new DiaCorte(72, d, RolCronograma.Principal));
            }
        }

        return new DatosCambioEstado(ProyectoId, "PRY-20261001-454325", estado, Inicio, finProyecto, personal, dias,
            [new ActividadCorte(1, 1, "DEV.01", Inicio, finProyecto)]);
    }
}

/// <summary>Repositorio: cada llamada a ObtenerAsync devuelve la siguiente respuesta (la última se repite).</summary>
internal sealed class RepositorioCambioFalso(params DatosCambioEstado?[] respuestas) : ICambioEstadoRepositorio
{
    public List<(int Id, int? Propietario, DateOnly Fecha)> Lecturas { get; } = [];
    public int UltimaVersion { get; set; } = 1;
    /// <summary>TAREA-19x: versiones que devuelven las próximas lecturas de la versión (después, UltimaVersion).</summary>
    public Queue<int> Versiones { get; } = new();

    /// <summary>TAREA-19x: orden de las lecturas ("version" / "datos").</summary>
    public List<string> Orden { get; } = [];
    public bool LanzarConflicto { get; set; }
    public CambioEstadoAplicar? Aplicado { get; private set; }

    public Task<DatosCambioEstado?> ObtenerAsync(int proyectoId, int? propietarioUsuarioId, DateOnly fecha, CancellationToken ct)
    {
        Lecturas.Add((proyectoId, propietarioUsuarioId, fecha));
        Orden.Add("datos");
        var respuesta = respuestas.Length == 0 ? null : respuestas[Math.Min(Lecturas.Count - 1, respuestas.Length - 1)];
        return Task.FromResult(respuesta);
    }

    public Task<int> ObtenerUltimaVersionEtapaAsync(int proyectoId, CancellationToken ct)
    {
        Orden.Add("version");
        return Task.FromResult(Versiones.Count > 0 ? Versiones.Dequeue() : UltimaVersion);
    }

    public Task AplicarAsync(CambioEstadoAplicar cambio, CancellationToken ct)
    {
        if (LanzarConflicto)
        {
            throw new ConflictoConcurrenciaException("RowVer");
        }
        Aplicado = cambio;
        return Task.CompletedTask;
    }
}

internal sealed class AdminFalso : IUsuarioActual
{
    public int? UsuarioId => 2;
    public string? Email => "admin.dev@profesiograma.local";
    public string? NombreMostrar => "ADMIN";
    public IReadOnlyCollection<string> Roles => [RolesApp.Admin];
    public bool EstaAutenticado => true;
    public bool TieneRol(string rol) => Roles.Contains(rol);
}

/// <summary>TAREA-19x: solicitudes de registro con el token de concurrencia (versión del repositorio falso o explícita).</summary>
internal static class ConVersionEstados
{
    public static CambioEstadoSolicitud ConVersion(this CambioEstadoSolicitud s, RepositorioCambioFalso repo) => s with { VersionProyecto = repo.UltimaVersion };
    public static CambioEstadoSolicitud ConVersion(this CambioEstadoSolicitud s, int version) => s with { VersionProyecto = version };
}
