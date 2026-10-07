using App.Application.Comun;
using App.Application.Seguridad;

namespace App.Application.Empleados;

/// <summary>
/// Búsqueda de empleados para asignar personal. Valida los parámetros y decide el filtro por departamentos
/// (FASE_5 §7.2 y P5). No es un control de seguridad: es una ayuda para acotar la búsqueda.
/// </summary>
public sealed class EmpleadoConsultaServicio(IEmpleadoConsultas consultas, IUsuarioActual usuario)
{
    public const int TamanoPorDefecto = 20;
    public const int TamanoMaximo = 100;

    public async Task<ResultadoConsulta<ResultadoBusquedaEmpleadosDto>> BuscarAsync(
        EmpleadoBusquedaSolicitud solicitud, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        var soloMisDepartamentos = LectorParametros.LeerBooleano(solicitud.SoloMisDepartamentos, "soloMisDepartamentos", true, errores);
        var pagina = LectorParametros.LeerPagina(solicitud.Pagina, errores);
        var tamano = LectorParametros.LeerTamano(solicitud.Tamano, TamanoPorDefecto, TamanoMaximo, errores);

        if (errores.Count > 0)
        {
            return ResultadoConsulta<ResultadoBusquedaEmpleadosDto>.Invalido(errores);
        }

        var departamentos = await ResolverDepartamentosAsync(soloMisDepartamentos, ct);
        var filtro = new EmpleadoFiltro(LectorParametros.Normalizar(solicitud.Texto), departamentos, pagina, tamano);

        return ResultadoConsulta<ResultadoBusquedaEmpleadosDto>.Ok(await consultas.BuscarAsync(filtro, ct));
    }

    /// <summary>
    /// soloMisDepartamentos = true: departamentos del usuario en UsuarioDepartamento (coincidencia POR NOMBRE con el
    /// departamento o la unidad del empleado). Si el usuario no tiene departamentos (p. ej. un admin) → todos (null).
    /// </summary>
    private async Task<IReadOnlyList<string>?> ResolverDepartamentosAsync(bool soloMisDepartamentos, CancellationToken ct)
    {
        if (!soloMisDepartamentos || usuario.UsuarioId is not int usuarioId)
        {
            return null;
        }

        var departamentos = await consultas.ObtenerDepartamentosDeUsuarioAsync(usuarioId, ct);
        return departamentos.Count == 0 ? null : departamentos;
    }
}
