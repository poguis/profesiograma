using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Erp;
using App.Application.Proyectos.Crear;
using App.Application.Seguridad;

namespace App.Application.Proyectos.Personal;

/// <summary>
/// TAREA-26d-3: GET /api/proyectos/{id}/personal/comparacion-erp. Compara los datos guardados en Empleado de TODAS las
/// personas guardadas del proyecto (la pantalla decide a cuáles los muestra: D3) con la lista de la API en caché
/// (10 min; lista anterior hasta 60 min). Solo informa (D1): no escribe. Con el ERP caído responde igual (sin avisos y
/// con <see cref="ComparacionEmpleadosErp.MensajeNoComparado"/>): nunca 503. Nunca lee la API en fresco.
/// </summary>
public sealed class ComparacionErpServicio(
    IEdicionPersonalRepositorio repositorio,
    IDatosReferenciaProyecto datos,
    IFuenteEmpleadosErp empleadosErp,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    /// <summary>null si el proyecto no existe o no es visible (misma visibilidad que GET …/edicion → 404).</summary>
    public async Task<ComparacionErpDto?> CompararAsync(int proyectoId, CancellationToken ct)
    {
        int? propietario = null;
        if (!usuario.TieneRol(RolesApp.Admin) && (propietario = usuario.UsuarioId) is null)
        {
            return null;
        }

        var proyecto = await repositorio.ObtenerAsync(proyectoId, propietario, FechaNegocio.Hoy(reloj), ct);
        if (proyecto is null)
        {
            return null;
        }

        var ids = proyecto.Personal.Select(p => p.EmpleadoId).Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new ComparacionErpDto(null, null, []); // sin personas guardadas: no hay nada que comparar ni API que leer
        }

        ListaEmpleadosErp lista;
        try
        {
            lista = await empleadosErp.ObtenerActivosAsync(ct);
        }
        catch (ErpNoDisponibleException)
        {
            return ComparacionEmpleadosErp.NoComparado();
        }

        return ComparacionEmpleadosErp.Comparar(await datos.ObtenerEmpleadosGuardadosAsync(ids, ct), lista);
    }
}
