namespace App.Application.Proyectos.Crear;

/// <summary>
/// Mínimo de personal (TAREA-19y, pendiente 33). Decisión del negocio (06/10/2026): en SharePoint más de la mitad de los
/// ~50 proyectos no tiene principal, solo backs, y deben migrarse tal cual y seguir siendo editables. Por eso el
/// principal es opcional y basta 1 persona (principal o back) en la creación, la actualización de personal y la
/// reactivación. El parámetro PROYECTO_EXIGE_PRINCIPAL = 1 vuelve a la regla C10 (TAREA-18): al menos 1 principal.
/// </summary>
public static class MinimoPersonal
{
    public const string ClavePersonal = "personal";
    public const string ClavePrincipales = "principales";
    public const string ClaveBacks = "backs";
    public const string MensajePersonas = "Se requiere al menos 1 persona (principal o back).";
    public const string MensajePrincipales = "Se requiere al menos 1 principal(es)."; // C10 / R5 (mismo texto que antes)

    /// <summary>Advertencia (no bloquea) cuando el proyecto resultante no tendrá ningún principal inicial (responsable).</summary>
    public const string AdvertenciaSinPrincipal = "El proyecto no tendrá principal: el responsable quedará vacío.";

    /// <summary>Error de mínimo (clave y mensaje) o null si se cumple.</summary>
    public static (string Clave, string Mensaje)? Validar(int principales, int backs, bool exigePrincipal)
    {
        if (exigePrincipal)
        {
            return principales < CrearProyectoValidador.MinimoPrincipales ? (ClavePrincipales, MensajePrincipales) : null;
        }

        return principales + backs < 1 ? (ClavePersonal, MensajePersonas) : null;
    }
}
