namespace App.Application.Seguridad;

/// <summary>
/// Valores de App Roles (Entra ID). DevAuth emite los mismos valores.
/// [PENDIENTE] Confirmar nombres definitivos al crear el App Registration de la API.
/// </summary>
public static class RolesApp
{
    public const string Admin = "Admin";
    public const string Gestor = "Gestor";
}

/// <summary>Nombres de políticas de autorización.</summary>
public static class Politicas
{
    /// <summary>Solo administradores (antes: SuperAdmin por correos fijos).</summary>
    public const string Admin = "Politica.Admin";

    /// <summary>Administradores o gestores de proyectos.</summary>
    public const string Gestor = "Politica.Gestor";
}
