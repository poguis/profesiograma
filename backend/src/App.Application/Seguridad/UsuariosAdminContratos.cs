using App.Application.Comun;

namespace App.Application.Seguridad;

// TAREA-25-1: administración de usuarios y de sus departamentos (UsuarioDepartamento). Solo Admin. Los roles NO se
// administran aquí (App Roles de Entra ID). Sin el usuario SISTEMA (EsSistema): no aparece y su Id da 404.

public sealed record DepartamentoUsuarioDto(int Id, string NombreCorto);

/// <summary>ConIdentidad = ya ingresó con Entra ID (tiene EntraObjectId). Departamentos = asignaciones activas.</summary>
public sealed record UsuarioAdminDto(
    int Id, string Email, string NombreMostrar, bool Activo, bool ConIdentidad, IReadOnlyList<DepartamentoUsuarioDto> Departamentos);

/// <summary>Activo = el departamento está activo (uno inactivo solo aparece si el usuario ya lo tiene asignado).</summary>
public sealed record DepartamentoDisponibleDto(int Id, string Nombre, string NombreCorto, bool Activo, bool Asignado);

public sealed record UsuarioAdminDetalleDto(
    int Id, string Email, string NombreMostrar, bool Activo, bool ConIdentidad, IReadOnlyList<DepartamentoUsuarioDto> Departamentos,
    IReadOnlyList<DepartamentoDisponibleDto> DepartamentosDisponibles, bool EsUsuarioActual);

/// <summary>GET /api/admin/usuarios (query string como texto, validado en el servicio).</summary>
public sealed record UsuarioAdminListadoSolicitud(string? Texto, string? IncluirInactivos, string? Pagina, string? Tamano);

public sealed record UsuarioAdminFiltro(string? Texto, bool IncluirInactivos, int Pagina, int Tamano);

public sealed record CrearUsuarioSolicitud(string? Email, string? NombreMostrar);

public sealed record UsuarioCreadoDto(int Id);

/// <summary>NombreAnterior y ActivoAnterior = los valores que vio el Admin (compare-and-set por valores).</summary>
public sealed record EditarUsuarioSolicitud(string? NombreMostrar, bool? Activo, string? NombreAnterior, bool? ActivoAnterior);

/// <summary>Anteriores = el conjunto de departamentos activos que vio el Admin.</summary>
public sealed record AsignarDepartamentosSolicitud(IReadOnlyList<int>? DepartamentoIds, IReadOnlyList<int>? Anteriores);

public sealed record DepartamentosAsignadosDto(IReadOnlyList<DepartamentoUsuarioDto> Departamentos);

/// <summary>Datos de un usuario para las reglas (sin el detalle de departamentos).</summary>
public sealed record UsuarioRegistro(int Id, string Email, Guid? EntraObjectId, string NombreMostrar, bool Activo, bool EsSistema);

public sealed record DepartamentoRegistro(int Id, string Nombre, string NombreCorto, bool Activo, short Orden);

/// <summary>Resultado de aplicar el conjunto: Conflicto = el conjunto activo ya no es el que vio el Admin.</summary>
public sealed record CambioDepartamentos(bool Conflicto, IReadOnlyList<int> Agregados, IReadOnlyList<int> Retirados);

/// <summary>Dominios de correo permitidos al dar de alta (configuración Usuarios:DominiosPermitidos; sin valor, sedemi.com).</summary>
public sealed record ConfiguracionUsuarios(IReadOnlyList<string> DominiosPermitidos)
{
    public static readonly IReadOnlyList<string> PorDefecto = ["sedemi.com"];
}

public interface IUsuariosAdminRepositorio
{
    Task<PaginaResultado<UsuarioAdminDto>> ListarAsync(UsuarioAdminFiltro filtro, CancellationToken ct);

    /// <summary>Usuario con sus asignaciones activas; null si no existe o es SISTEMA.</summary>
    Task<UsuarioAdminDto?> ObtenerAsync(int id, CancellationToken ct);

    /// <summary>null si no existe (incluye SISTEMA: la regla la aplica el servicio).</summary>
    Task<UsuarioRegistro?> ObtenerRegistroAsync(int id, CancellationToken ct);

    Task<UsuarioRegistro?> BuscarPorEmailAsync(string email, CancellationToken ct);

    Task<IReadOnlyList<DepartamentoRegistro>> ObtenerDepartamentosAsync(CancellationToken ct);

    /// <summary>Alta (auditoría por el AuditoriaInterceptor). null si otro lo creó a la vez (UQ_Usuario_Email).</summary>
    Task<int?> CrearAsync(string email, string nombreMostrar, CancellationToken ct);

    /// <summary>
    /// UPDATE … WHERE Id AND NombreMostrar = nombreAnterior AND Activo = activoAnterior AND EsSistema = 0 (ExecuteUpdate,
    /// compare-and-set), con ModificadoPorId y FechaModificacion explícitos. Devuelve las filas afectadas (0 = otro lo cambió).
    /// </summary>
    Task<int> ActualizarAsync(int id, string nombreAnterior, bool activoAnterior, string nombre, bool activo, int usuarioId,
        DateTime fechaUtc, CancellationToken ct);

    /// <summary>
    /// En una transacción serializada por usuario (sp_getapplock 'profesiograma:usuario:{id}'; espera agotada → 503): relee
    /// las asignaciones y, si el conjunto activo es <paramref name="anteriores"/>, retira (Activo = 0), agrega o reactiva
    /// (Notificado = 0, FechaNotificacion NULL) sin tocar las que no cambian. Nunca borra filas.
    /// </summary>
    Task<CambioDepartamentos> AsignarDepartamentosAsync(int usuarioId, IReadOnlySet<int> anteriores, IReadOnlySet<int> nuevos,
        CancellationToken ct);
}
