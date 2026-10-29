using System.Net.Mail;
using App.Application.Comun;

namespace App.Application.Seguridad;

public enum EstadoAdminUsuario { Ok, Invalido, NoEncontrado, Conflicto }

/// <param name="UsuarioExistenteId">409 del alta: Id del usuario con ese correo (para abrirlo); null si es SISTEMA.</param>
/// <param name="Agregados">Departamentos agregados o reactivados (solo para el log).</param>
/// <param name="Retirados">Departamentos retirados (solo para el log).</param>
public sealed record ResultadoAdminUsuario<T>(
    EstadoAdminUsuario Estado, T? Valor = default, IReadOnlyDictionary<string, string[]>? Errores = null,
    int? UsuarioExistenteId = null, IReadOnlyList<int>? Agregados = null, IReadOnlyList<int>? Retirados = null);

/// <summary>
/// TAREA-25-1: usuarios y departamentos (Admin). Alta por correo (precarga: el oid se completa en el primer ingreso),
/// nombre, baja y reactivación lógicas (compare-and-set por valores) y conjunto de departamentos (compare-and-set por
/// conjunto, serializado por usuario). Toda escritura de un usuario invalida su caché de provisionamiento (efecto inmediato).
/// </summary>
public sealed class UsuariosAdminServicio(
    IUsuariosAdminRepositorio repositorio, IUsuarioProvisionamiento provisionamiento, IUsuarioActual usuario,
    ConfiguracionUsuarios configuracion, TimeProvider reloj)
{
    public const int TamanoPorDefecto = 25, TamanoMaximo = 100;
    public const int LargoEmail = 256, LargoNombre = 200; // Usuario.Email NVARCHAR(256), NombreMostrar NVARCHAR(200) NOT NULL

    public const string MensajeEmailVacio = "El correo es obligatorio.";
    public const string MensajeEmailLargo = "El correo supera los 256 caracteres.";
    public const string MensajeEmailFormato = "El correo no tiene un formato válido.";
    public const string MensajeNombreVacio = "El nombre es obligatorio.";
    public const string MensajeNombreLargo = "El nombre supera los 200 caracteres.";
    public const string MensajeEmailDuplicado = "Ya existe un usuario con ese correo.";
    public const string MensajeActivoFalta = "Indique si el usuario está activo.";
    public const string MensajeAnterioresUsuario = "Faltan los valores anteriores del usuario; recargue.";
    public const string MensajeBajaPropia = "No puedes darte de baja a ti mismo.";
    public const string MensajeConflictoUsuario = "Otro usuario cambió el registro; recargue.";
    public const string MensajeListaFalta = "Envíe la lista de departamentos (puede estar vacía).";
    public const string MensajeAnterioresDepartamentos = "Falta el conjunto anterior de departamentos; recargue.";
    public const string MensajeConflictoDepartamentos = "Otro usuario cambió los departamentos; recargue.";
    public static string MensajeDominio(IEnumerable<string> dominios) => $"El correo debe ser de un dominio permitido: {string.Join(", ", dominios)}.";
    public static string MensajeDepartamentoRepetido(int id) => $"El departamento {id} está repetido.";
    public static string MensajeDepartamentoInvalido(int id) => $"El departamento {id} no existe o está inactivo.";

    public async Task<ResultadoConsulta<PaginaResultado<UsuarioAdminDto>>> ListarAsync(UsuarioAdminListadoSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var errores = new Dictionary<string, string[]>();
        var incluirInactivos = LectorParametros.LeerBooleano(s.IncluirInactivos, "incluirInactivos", false, errores);
        var pagina = LectorParametros.LeerPagina(s.Pagina, errores);
        var tamano = LectorParametros.LeerTamano(s.Tamano, TamanoPorDefecto, TamanoMaximo, errores);
        if (errores.Count > 0)
        {
            return ResultadoConsulta<PaginaResultado<UsuarioAdminDto>>.Invalido(errores);
        }

        var filtro = new UsuarioAdminFiltro(LectorParametros.Normalizar(s.Texto), incluirInactivos, pagina, tamano);
        return ResultadoConsulta<PaginaResultado<UsuarioAdminDto>>.Ok(await repositorio.ListarAsync(filtro, ct));
    }

    /// <summary>null = no existe o es SISTEMA (404). Disponibles: los activos más los inactivos ya asignados, por Orden.</summary>
    public async Task<UsuarioAdminDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct)
    {
        if (await repositorio.ObtenerAsync(id, ct) is not { } u)
        {
            return null;
        }

        var asignados = u.Departamentos.Select(d => d.Id).ToHashSet();
        var disponibles = (await repositorio.ObtenerDepartamentosAsync(ct))
            .Where(d => d.Activo || asignados.Contains(d.Id))
            .OrderBy(d => d.Orden).ThenBy(d => d.Id)
            .Select(d => new DepartamentoDisponibleDto(d.Id, d.Nombre, d.NombreCorto, d.Activo, asignados.Contains(d.Id)))
            .ToList();
        return new UsuarioAdminDetalleDto(u.Id, u.Email, u.NombreMostrar, u.Activo, u.ConIdentidad, u.Departamentos, disponibles,
            usuario.UsuarioId == u.Id);
    }

    public async Task<ResultadoAdminUsuario<UsuarioCreadoDto>> CrearAsync(CrearUsuarioSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var errores = new Dictionary<string, List<string>>();
        var email = ValidarEmail(s.Email, errores);
        var nombre = ValidarNombre(s.NombreMostrar, errores);
        if (errores.Count > 0)
        {
            return Invalido<UsuarioCreadoDto>(errores);
        }

        if (await repositorio.BuscarPorEmailAsync(email!, ct) is { } existente)
        {
            return Duplicado(existente);
        }

        if (await repositorio.CrearAsync(email!, nombre!, ct) is not int id)
        {
            // Otro Admin (o el primer ingreso de esa persona) lo creó entre la lectura y el INSERT.
            return await repositorio.BuscarPorEmailAsync(email!, ct) is { } creado
                ? Duplicado(creado)
                : new ResultadoAdminUsuario<UsuarioCreadoDto>(EstadoAdminUsuario.Conflicto);
        }

        provisionamiento.Invalidar(email!, null);
        return new ResultadoAdminUsuario<UsuarioCreadoDto>(EstadoAdminUsuario.Ok, new UsuarioCreadoDto(id));
    }

    public async Task<ResultadoAdminUsuario<UsuarioAdminDto>> EditarAsync(int id, EditarUsuarioSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (await repositorio.ObtenerRegistroAsync(id, ct) is not { EsSistema: false } registro)
        {
            return new ResultadoAdminUsuario<UsuarioAdminDto>(EstadoAdminUsuario.NoEncontrado);
        }

        var errores = new Dictionary<string, List<string>>();
        var nombre = ValidarNombre(s.NombreMostrar, errores);
        if (s.Activo is null)
        {
            Agregar(errores, "activo", MensajeActivoFalta);
        }
        else if (s.Activo == false && usuario.UsuarioId == id)
        {
            Agregar(errores, "activo", MensajeBajaPropia);
        }

        if (s.NombreAnterior is null || s.ActivoAnterior is null)
        {
            Agregar(errores, "anterior", MensajeAnterioresUsuario);
        }

        if (errores.Count > 0)
        {
            return Invalido<UsuarioAdminDto>(errores);
        }

        var filas = await repositorio.ActualizarAsync(id, s.NombreAnterior!, s.ActivoAnterior!.Value, nombre!, s.Activo!.Value,
            usuario.UsuarioId ?? Domain.Seguridad.Usuario.IdSistema, FechaAuditoria(), ct);
        if (filas == 0)
        {
            return new ResultadoAdminUsuario<UsuarioAdminDto>(EstadoAdminUsuario.Conflicto);
        }

        provisionamiento.Invalidar(registro.Email, registro.EntraObjectId); // P6: la baja rige en la siguiente solicitud
        return new ResultadoAdminUsuario<UsuarioAdminDto>(EstadoAdminUsuario.Ok, await repositorio.ObtenerAsync(id, ct));
    }

    public async Task<ResultadoAdminUsuario<DepartamentosAsignadosDto>> AsignarDepartamentosAsync(
        int id, AsignarDepartamentosSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (await repositorio.ObtenerRegistroAsync(id, ct) is not { EsSistema: false })
        {
            return new ResultadoAdminUsuario<DepartamentosAsignadosDto>(EstadoAdminUsuario.NoEncontrado);
        }

        var errores = new Dictionary<string, List<string>>();
        if (s.DepartamentoIds is null)
        {
            Agregar(errores, "departamentoIds", MensajeListaFalta);
        }

        if (s.Anteriores is null)
        {
            Agregar(errores, "anteriores", MensajeAnterioresDepartamentos);
        }

        var anteriores = (s.Anteriores ?? []).ToHashSet();
        var nuevos = new HashSet<int>();
        var departamentos = (await repositorio.ObtenerDepartamentosAsync(ct)).ToDictionary(d => d.Id);
        foreach (var depId in s.DepartamentoIds ?? [])
        {
            if (!nuevos.Add(depId))
            {
                Agregar(errores, "departamentoIds", MensajeDepartamentoRepetido(depId));
            }
            // Un inactivo solo se admite si ya estaba asignado (se conserva); la transacción comprueba que el conjunto
            // actual sigue siendo «anteriores».
            else if (!departamentos.TryGetValue(depId, out var d) || (!d.Activo && !anteriores.Contains(depId)))
            {
                Agregar(errores, "departamentoIds", MensajeDepartamentoInvalido(depId));
            }
        }

        if (errores.Count > 0)
        {
            return Invalido<DepartamentosAsignadosDto>(errores);
        }

        var cambio = await repositorio.AsignarDepartamentosAsync(id, anteriores, nuevos, ct);
        if (cambio.Conflicto)
        {
            return new ResultadoAdminUsuario<DepartamentosAsignadosDto>(EstadoAdminUsuario.Conflicto);
        }

        var resultado = nuevos.Select(n => departamentos[n]).OrderBy(d => d.Orden).ThenBy(d => d.Id)
            .Select(d => new DepartamentoUsuarioDto(d.Id, d.NombreCorto)).ToList();
        return new ResultadoAdminUsuario<DepartamentosAsignadosDto>(EstadoAdminUsuario.Ok, new DepartamentosAsignadosDto(resultado),
            Agregados: cambio.Agregados, Retirados: cambio.Retirados);
    }

    /// <summary>Recorta y pasa a minúsculas; exige que MailAddress lea exactamente esa dirección y un dominio permitido.</summary>
    private string? ValidarEmail(string? valor, Dictionary<string, List<string>> errores)
    {
        var email = valor?.Trim().ToLowerInvariant();
        var dominios = configuracion.DominiosPermitidos.Count > 0 ? configuracion.DominiosPermitidos : ConfiguracionUsuarios.PorDefecto;
        if (string.IsNullOrEmpty(email))
        {
            Agregar(errores, "email", MensajeEmailVacio);
        }
        else if (email.Length > LargoEmail)
        {
            Agregar(errores, "email", MensajeEmailLargo);
        }
        else if (!MailAddress.TryCreate(email, out var direccion) || direccion.Address != email)
        {
            Agregar(errores, "email", MensajeEmailFormato);
        }
        else if (!dominios.Contains(direccion.Host, StringComparer.OrdinalIgnoreCase))
        {
            Agregar(errores, "email", MensajeDominio(dominios));
        }

        return email;
    }

    private static string? ValidarNombre(string? valor, Dictionary<string, List<string>> errores)
    {
        var nombre = valor?.Trim();
        if (string.IsNullOrEmpty(nombre))
        {
            Agregar(errores, "nombreMostrar", MensajeNombreVacio);
        }
        else if (nombre.Length > LargoNombre)
        {
            Agregar(errores, "nombreMostrar", MensajeNombreLargo);
        }

        return nombre;
    }

    private DateTime FechaAuditoria()
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        return new DateTime(ahora.Ticks - (ahora.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc); // DATETIME2(0)
    }

    private static ResultadoAdminUsuario<UsuarioCreadoDto> Duplicado(UsuarioRegistro existente) =>
        new(EstadoAdminUsuario.Conflicto, UsuarioExistenteId: existente.EsSistema ? null : existente.Id);

    private static void Agregar(Dictionary<string, List<string>> e, string clave, string mensaje)
    {
        if (!e.TryGetValue(clave, out var lista))
        {
            e[clave] = lista = [];
        }

        lista.Add(mensaje);
    }

    private static ResultadoAdminUsuario<T> Invalido<T>(Dictionary<string, List<string>> e) =>
        new(EstadoAdminUsuario.Invalido, Errores: e.ToDictionary(x => x.Key, x => x.Value.ToArray()));
}
