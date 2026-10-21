using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Proyectos.Crear;
using App.Application.Seguridad;
using App.Domain.Catalogos;
using App.Domain.Novedades;

namespace App.Application.Novedades;

/// <summary>
/// Validación de "Crear novedad" (TAREA-23-1). 400 por campo con mensajes en español; GENERAL sin permiso → 403 (antes
/// de validar lo demás). La API de empleados (lectura FRESCA, como al registrar un proyecto) y la base se consultan solo
/// si el resto de la solicitud es válido: un 400 no gasta una llamada a la API. API caída → ErpNoDisponibleException (503).
/// </summary>
public sealed class NovedadValidador(
    INovedadConsultas consultas,
    IDatosReferenciaProyecto datos,
    IFuenteEmpleadosErp empleados,
    IUsuarioActual usuario)
{
    public const int LargoObservacion = 500;

    public const string MensajeAplicacionObligatoria = "El tipo de aplicación es obligatorio.";
    public const string MensajeAplicacionInvalida = "El tipo de aplicación no es válido.";
    public const string MensajeTipoObligatorio = "El tipo de novedad es obligatorio.";
    public const string MensajeTipoInvalido = "El tipo de novedad no es válido.";
    public const string MensajeEmpleadoInvalido = "El empleado no existe o no está activo.";
    public const string MensajeProyectoObligatorio = "El proyecto es obligatorio.";
    public const string MensajeProyectoInvalido = "El proyecto no existe, no está vigente o no tienes acceso.";
    public const string MensajeInicioObligatorio = "La fecha de inicio es obligatoria.";
    public const string MensajeFinObligatorio = "La fecha fin es obligatoria.";
    public const string MensajeFinMenor = "La fecha fin debe ser mayor o igual a la fecha inicio.";
    public static readonly string MensajeMaxDias = $"La novedad no puede superar {DiasNovedad.MaxDias} días.";
    public static readonly string MensajeObservacionLarga = $"La observación no puede superar {LargoObservacion} caracteres.";

    public static string MensajeTipoNoAplica(string aplicacion) => $"El tipo de novedad no aplica a {aplicacion}.";

    public static string MensajeNoCorresponde(string aplicacion) => $"No corresponde en una novedad {aplicacion}.";

    /// <summary>Resultado: Valida (sin errores), Errores (400) o Prohibido (403).</summary>
    public sealed record Validacion(NovedadValidada? Valida, IReadOnlyDictionary<string, string[]> Errores, bool Prohibido = false);

    public async Task<Validacion> ValidarAsync(CrearNovedadSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var e = new Dictionary<string, string[]>();

        // Aplicación (y GENERAL sin permiso → 403 antes de todo lo demás).
        var aplicacionId = AplicacionNovedad.Id(s.TipoAplicacion);
        if (aplicacionId is null)
        {
            e["tipoAplicacion"] = [LectorParametros.Normalizar(s.TipoAplicacion) is null ? MensajeAplicacionObligatoria : MensajeAplicacionInvalida];
        }
        else if (aplicacionId == CatalogoIds.TipoAplicacionNovedad.General && !PermisosNovedad.PuedeRegistrarGeneral(usuario))
        {
            return new Validacion(null, e, Prohibido: true);
        }

        var aplicacion = aplicacionId is byte a ? AplicacionNovedad.Codigo(a) : null;

        // Tipo de novedad (RN16: SeleccionableManual y bandera de la aplicación).
        TipoNovedadRef? tipo = null;
        var codigoTipo = LectorParametros.Normalizar(s.TipoNovedad);
        if (codigoTipo is null)
        {
            e["tipoNovedad"] = [MensajeTipoObligatorio];
        }
        else
        {
            tipo = (await consultas.ObtenerTiposAsync(ct))
                .FirstOrDefault(t => string.Equals(t.Codigo, codigoTipo, StringComparison.OrdinalIgnoreCase));
            if (tipo is null)
            {
                e["tipoNovedad"] = [MensajeTipoInvalido];
            }
            else if (aplicacionId is byte id && !tipo.EsElegible(id))
            {
                e["tipoNovedad"] = [MensajeTipoNoAplica(aplicacion!)];
            }
        }

        // Sujeto: PERSONA → codigoEkon; PROYECTO → proyectoId; GENERAL → ninguno.
        var codigoEkon = MapeoEmpleado.Normalizar(s.CodigoEkon);
        if (aplicacionId == CatalogoIds.TipoAplicacionNovedad.Persona && codigoEkon is null)
        {
            e["codigoEkon"] = [CatalogoEmpleados.MensajeObligatorio];
        }
        else if (aplicacionId is byte ap && ap != CatalogoIds.TipoAplicacionNovedad.Persona && codigoEkon is not null)
        {
            e["codigoEkon"] = [MensajeNoCorresponde(aplicacion!)];
        }

        if (aplicacionId == CatalogoIds.TipoAplicacionNovedad.Proyecto && s.ProyectoId is null)
        {
            e["proyectoId"] = [MensajeProyectoObligatorio];
        }
        else if (aplicacionId is byte ap && ap != CatalogoIds.TipoAplicacionNovedad.Proyecto && s.ProyectoId is not null)
        {
            e["proyectoId"] = [MensajeNoCorresponde(aplicacion!)];
        }

        // Fechas: obligatorias, fin ≥ inicio, máximo DiasNovedad.MaxDias días. Se permiten fechas pasadas.
        if (s.FechaInicio is null)
        {
            e["fechaInicio"] = [MensajeInicioObligatorio];
        }

        if (s.FechaFin is null)
        {
            e["fechaFin"] = [MensajeFinObligatorio];
        }
        else if (s.FechaInicio is DateOnly inicio && s.FechaFin is DateOnly fin)
        {
            if (fin < inicio)
            {
                e["fechaFin"] = [MensajeFinMenor];
            }
            else if (DiasNovedad.Contar(inicio, fin) > DiasNovedad.MaxDias)
            {
                e["fechaFin"] = [MensajeMaxDias];
            }
        }

        var observacion = LectorParametros.Normalizar(s.Observacion);
        if (observacion is { Length: > LargoObservacion })
        {
            e["observacion"] = [MensajeObservacionLarga];
        }

        if (e.Count > 0)
        {
            return new Validacion(null, e);
        }

        // Validaciones contra la API y la base (la solicitud ya es válida en forma).
        EmpleadoAsignable? empleado = null;
        var cedulas = CedulasEmpleados.Vacio;
        if (aplicacionId == CatalogoIds.TipoAplicacionNovedad.Persona)
        {
            var catalogo = await CatalogoEmpleados.CargarAsync(datos, empleados, [codigoEkon], fresco: true, ct);
            empleado = catalogo.Validar(codigoEkon, "codigoEkon", new Dictionary<string, List<string>>());
            if (empleado is null)
            {
                return new Validacion(null, new Dictionary<string, string[]> { ["codigoEkon"] = [MensajeEmpleadoInvalido] });
            }

            cedulas = catalogo.Cedulas;
        }

        if (aplicacionId == CatalogoIds.TipoAplicacionNovedad.Proyecto)
        {
            if (!PermisosNovedad.TryObtenerVisibilidad(usuario, out var propietario)
                || !await consultas.ProyectoElegibleAsync(s.ProyectoId!.Value, propietario, ct))
            {
                return new Validacion(null, new Dictionary<string, string[]> { ["proyectoId"] = [MensajeProyectoInvalido] });
            }
        }

        return new Validacion(
            new NovedadValidada(aplicacionId!.Value, tipo!, empleado, s.ProyectoId, s.FechaInicio!.Value, s.FechaFin!.Value, observacion, cedulas),
            e);
    }
}
