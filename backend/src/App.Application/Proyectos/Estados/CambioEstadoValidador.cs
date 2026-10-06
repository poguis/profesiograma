using System.Globalization;
using App.Application.Comun;
using App.Domain.Proyectos.Estados;

namespace App.Application.Proyectos.Estados;

/// <summary>Movimiento validado (Errores vacío) o los errores por campo.</summary>
public sealed record ResultadoValidacionCambio(
    MovimientoEstado? Movimiento, string? EstadoDestino, DateOnly? Fecha, IReadOnlyDictionary<string, string[]> Errores)
{
    public bool EsValido => Errores.Count == 0;
}

/// <summary>
/// Validación del cambio de estado (E1, E2). Lógica pura sobre los datos ya leídos del proyecto.
/// Alcance TAREA-14: SUSPENSION y CIERRE. La REACTIVACION responde 400: se registra con POST …/reactivacion (TAREA-17b).
/// </summary>
public sealed class CambioEstadoValidador
{
    public const string MensajeReactivacion = "La reactivación se registra con la opción Reactivar.";
    public const string MensajeFechaObligatoria = "La fecha del movimiento es obligatoria.";

    public ResultadoValidacionCambio Validar(CambioEstadoSolicitud s, string estadoActual, DateOnly inicioProyecto, DateOnly finProyecto)
    {
        ArgumentNullException.ThrowIfNull(s);
        var e = new Dictionary<string, List<string>>();
        var origen = estadoActual.Trim().ToUpperInvariant();

        // --- Estado destino (E1)
        MovimientoEstado? movimiento = null;
        var destino = LectorParametros.Normalizar(s.EstadoDestino)?.ToUpperInvariant();
        if (destino is null)
        {
            Agregar(e, "estadoDestino", "El estado destino es obligatorio.");
        }
        else if (!MaquinaEstadosProyecto.EsConocido(destino))
        {
            Agregar(e, "estadoDestino", $"El estado destino '{destino}' no existe.");
        }
        else
        {
            movimiento = MaquinaEstadosProyecto.Resolver(origen, destino);
            switch (movimiento)
            {
                case MovimientoEstado.SinCambio:
                    Agregar(e, "estadoDestino", $"El proyecto ya está en estado {origen}.");
                    break;
                case MovimientoEstado.Reactivacion:
                    Agregar(e, "estadoDestino", MensajeReactivacion);
                    break;
                case MovimientoEstado.NoPermitido:
                    Agregar(e, "estadoDestino", $"Este cambio de estado no está permitido desde el estado actual ({origen} → {destino}).");
                    break;
            }
        }

        // --- Fecha del movimiento (E2): obligatoria e inclusiva en [inicio, fin] del proyecto.
        // C11 (TAREA-18): solo se valida si el movimiento es válido (SUSPENSION o CIERRE); con un destino
        // inválido (SIN_CAMBIO, NO_PERMITIDO, REACTIVACION o desconocido) basta el error de estadoDestino.
        if (movimiento is MovimientoEstado.Suspension or MovimientoEstado.Cierre)
        {
            if (s.Fecha is not DateOnly fecha)
            {
                Agregar(e, "fecha", MensajeFechaObligatoria);
            }
            else if (fecha < inicioProyecto)
            {
                Agregar(e, "fecha", $"La fecha del movimiento no puede ser menor a la fecha de inicio del proyecto ({Formato(inicioProyecto)}).");
            }
            else if (fecha > finProyecto)
            {
                Agregar(e, "fecha", $"La fecha del movimiento no puede ser mayor a la fecha fin del proyecto ({Formato(finProyecto)}).");
            }
        }

        var errores = e.ToDictionary(x => x.Key, x => x.Value.ToArray());
        return new ResultadoValidacionCambio(movimiento, destino, s.Fecha, errores);
    }

    private static string Formato(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static void Agregar(Dictionary<string, List<string>> e, string clave, string mensaje)
    {
        if (!e.TryGetValue(clave, out var lista))
        {
            e[clave] = lista = [];
        }
        lista.Add(mensaje);
    }
}
