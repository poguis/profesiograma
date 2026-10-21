using System.Linq.Expressions;
using App.Application.Seguridad;
using App.Domain.Catalogos;
using App.Domain.Novedades;

namespace App.Application.Novedades;

/// <summary>
/// ÚNICO punto de decisión de los permisos de novedades (TAREA-23-1). Cambiar quién registra GENERAL (J1: hoy solo
/// Admin; a futuro una bandera o un departamento, N1-B/C) o quién ve qué (J2) se hace solo aquí: los endpoints, los
/// servicios y las consultas de Infrastructure (que aplican <see cref="VisiblePara"/>) no cambian.
/// </summary>
public static class PermisosNovedad
{
    public const string MensajeSinPermiso = "No tienes permiso para modificar esta novedad.";
    public const string MensajeAnulada = "La novedad está anulada.";

    /// <summary>J1: solo el rol Admin registra novedades GENERAL.</summary>
    public static bool PuedeRegistrarGeneral(IUsuarioActual usuario) => usuario.TieneRol(RolesApp.Admin);

    /// <summary>
    /// J2: true = ve todas (Admin; <paramref name="usuarioId"/> = null). Si no, solo las de <see cref="VisiblePara"/>
    /// (usuarioId). False = sin usuario resuelto: no ve ninguna (no debería ocurrir en endpoints autenticados).
    /// </summary>
    public static bool TryObtenerVisibilidad(IUsuarioActual usuario, out int? usuarioId)
    {
        if (usuario.TieneRol(RolesApp.Admin))
        {
            usuarioId = null;
            return true;
        }

        usuarioId = usuario.UsuarioId;
        return usuarioId is not null;
    }

    /// <summary>
    /// J2 (no Admin): las que creó, las PROYECTO de sus proyectos visibles (R1: propietario, no eliminado) y todas las
    /// GENERAL; nunca las de origen PERMISOS_MEDICOS ni las PERSONA de otros. Se traduce a SQL (Infrastructure).
    /// </summary>
    public static Expression<Func<Novedad, bool>> VisiblePara(int usuarioId) => n =>
        n.OrigenNovedadId != CatalogoIds.OrigenNovedad.PermisosMedicos
        && (n.TipoAplicacionNovedadId == CatalogoIds.TipoAplicacionNovedad.General
            || n.CreadoPorId == usuarioId
            || (n.TipoAplicacionNovedadId == CatalogoIds.TipoAplicacionNovedad.Proyecto
                && n.Proyecto != null && !n.Proyecto.Eliminado && n.Proyecto.PropietarioUsuarioId == usuarioId));

    /// <summary>
    /// N3: quién puede editar o anular (la 23-2 implementa las acciones; aquí solo se informa). Origen no editable
    /// (PERMISOS_MEDICOS) o anulada → nadie, con el motivo. Admin: todas. PERSONA: el creador. PROYECTO: el propietario
    /// del proyecto. GENERAL: quien puede registrar GENERAL.
    /// </summary>
    public static (bool Puede, string? Motivo) Modificacion(IUsuarioActual usuario, NovedadDetalleDatos n)
    {
        if (!n.OrigenEditable)
        {
            return (false, MensajeSoloLectura(n.OrigenCodigo));
        }

        if (n.Anulada)
        {
            return (false, MensajeAnulada);
        }

        return PuedeModificar(usuario, n.AplicacionId, n.CreadoPorId, n.PropietarioProyectoId) ? (true, null) : (false, MensajeSinPermiso);
    }

    /// <summary>
    /// N3, solo el permiso (sin origen ni anulación): Admin; PERSONA el creador; PROYECTO el propietario del proyecto;
    /// GENERAL quien puede registrar GENERAL. La edición y la anulación (TAREA-23-2) lo usan para el 403.
    /// </summary>
    public static bool PuedeModificar(IUsuarioActual usuario, byte aplicacionId, int creadoPorId, int? propietarioProyectoId) =>
        usuario.TieneRol(RolesApp.Admin) || aplicacionId switch
        {
            CatalogoIds.TipoAplicacionNovedad.Persona => usuario.UsuarioId == creadoPorId,
            CatalogoIds.TipoAplicacionNovedad.Proyecto => usuario.UsuarioId is int u && u == propietarioProyectoId,
            CatalogoIds.TipoAplicacionNovedad.General => PuedeRegistrarGeneral(usuario),
            _ => false,
        };

    /// <summary>Mensaje del origen no editable (400 en `origen` al editar o anular; motivo en el detalle).</summary>
    public static string MensajeSoloLectura(string origenCodigo) => $"Las novedades de {origenCodigo} son de solo lectura.";

    /// <summary>El registrante (nombre y correo de quien registró, o de la app externa) solo lo ve Admin.</summary>
    public static bool VeRegistrante(IUsuarioActual usuario) => usuario.TieneRol(RolesApp.Admin);
}
