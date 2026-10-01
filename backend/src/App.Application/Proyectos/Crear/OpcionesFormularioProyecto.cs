using App.Application.Seguridad;

namespace App.Application.Proyectos.Crear;

/// <summary>
/// GET /api/proyectos/opciones-formulario: lo que el formulario "Nuevo proyecto" necesita y el gestor no puede leer
/// de /api/admin/parametros. Mismas fuentes que CrearProyectoValidador (la autoridad sigue siendo el servidor).
/// </summary>
/// <param name="Departamentos">Departamentos activos del usuario (P5): 0 = no se muestra, 1 = automático, varios = elige.</param>
/// <param name="AlmuerzoSalidaOpciones">"HH:mm" desde ReglasAlmuerzo (RN09).</param>
/// <param name="AlmuerzoRegresoOpciones">"HH:mm" desde ReglasAlmuerzo (RN09).</param>
/// <param name="MaxPrincipales">Parametro PROYECTO_MAX_PRINCIPALES.</param>
/// <param name="MaxBacks">Parametro PROYECTO_MAX_BACKS.</param>
/// <param name="BackMaxDiasDescanso">Parametro BACK_MAX_DIAS_DESCANSO.</param>
public sealed record OpcionesFormularioProyectoDto(
    IReadOnlyList<DepartamentoRef> Departamentos,
    IReadOnlyList<string> AlmuerzoSalidaOpciones,
    IReadOnlyList<string> AlmuerzoRegresoOpciones,
    int MaxPrincipales,
    int MaxBacks,
    int BackMaxDiasDescanso);

public sealed class OpcionesFormularioProyectoServicio(IDatosReferenciaProyecto datos, IUsuarioActual usuario)
{
    public async Task<OpcionesFormularioProyectoDto> ObtenerAsync(CancellationToken ct)
    {
        // Mismas consultas que el validador: ObtenerDepartamentosDeUsuarioAsync (P5) y ObtenerLimitesAsync (RN18).
        var departamentos = usuario.UsuarioId is int usuarioId
            ? await datos.ObtenerDepartamentosDeUsuarioAsync(usuarioId, ct)
            : [];
        var limites = await datos.ObtenerLimitesAsync(ct);

        return new OpcionesFormularioProyectoDto(
            departamentos,
            ReglasAlmuerzo.OpcionesSalida,
            ReglasAlmuerzo.OpcionesRegreso,
            limites.MaxPrincipales,
            limites.MaxBacks,
            limites.MaxDiasDescansoBack);
    }
}
