using System.Text.Json;

namespace App.Application.Proyectos;

/// <summary>Persona en el snapshot JSON de una etapa. Sin cédula ni correo.</summary>
/// <param name="Rol">PRINCIPAL | BACK</param>
/// <param name="TipoRegistro">JORNADA | DESCANSO (los principales siempre JORNADA).</param>
public sealed record ElementoSnapshot(
    short Numero,
    string Rol,
    string Ekon,
    string Nombre,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? Jornada,
    byte? DiasTrabajo,
    byte DiasDescanso,
    string TipoRegistro);

/// <summary>
/// Snapshot JSON del personal de una etapa (ProyectoEtapa.SnapshotPersonal). Mismo formato para la creación (RN12)
/// y para suspensión y cierre (E4): número, rol, EKON, nombre, fechas, jornada, días y tipo de registro.
/// </summary>
public static class SnapshotPersonal
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public static string Serializar(IEnumerable<ElementoSnapshot> personal) =>
        JsonSerializer.Serialize(personal, Opciones);
}
