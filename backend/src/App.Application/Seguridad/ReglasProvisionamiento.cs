namespace App.Application.Seguridad;

/// <summary>Lo que el alta necesita de una fila Usuario ya guardada.</summary>
public sealed record UsuarioIdentidad(int Id, Guid? EntraObjectId, string? Upn, bool Activo);

/// <summary>Identidad del token, ya normalizada (UPN y correo recortados y en minúsculas).</summary>
public sealed record IdentidadToken(Guid? EntraObjectId, string? Upn, string Correo);

public enum TipoDecisionProvisionamiento
{
    /// <summary>Usar <see cref="DecisionProvisionamiento.Usuario"/>, completando el oid o el UPN si se indica.</summary>
    Encontrado,
    /// <summary>No existe: crear la fila con el oid, el UPN y el correo del token.</summary>
    Crear,
    /// <summary>La fila pertenece a otra identidad: 403 con <see cref="DecisionProvisionamiento.Motivo"/>.</summary>
    Conflicto,
}

public sealed record DecisionProvisionamiento(
    TipoDecisionProvisionamiento Tipo,
    UsuarioIdentidad? Usuario = null,
    bool CompletarOid = false,
    bool CompletarUpn = false,
    string? Motivo = null);

/// <summary>
/// TAREA-7b-2 (P3): en SEDEMI el UPN (preferred_username) suele ser distinto del correo con el que el Admin precarga. Orden
/// de búsqueda: oid → UPN → correo. Al encontrar se completan el oid y el UPN que falten (nunca se reemplazan, Q4). Lógica
/// pura: Infrastructure busca y guarda.
/// </summary>
public static class ReglasProvisionamiento
{
    public const string MotivoCorreoOtroOid = "El correo está asociado a otra identidad de Entra ID.";
    public const string MotivoUpnOtroOid = "El UPN está asociado a otra identidad de Entra ID.";
    public const string MotivoCorreoOtroUpn = "El correo está asociado a otro UPN de Entra ID.";

    /// <param name="porOid">Fila con el oid del token (null si no hay oid o no existe).</param>
    /// <param name="porUpn">Fila con el UPN del token; hace falta si no hay fila por oid o si a esa le falta el UPN.</param>
    /// <param name="porCorreo">Fila con el correo del token; hace falta solo si no hubo fila por oid ni por UPN.</param>
    public static DecisionProvisionamiento Decidir(IdentidadToken token, UsuarioIdentidad? porOid, UsuarioIdentidad? porUpn,
        UsuarioIdentidad? porCorreo)
    {
        if (porOid is not null)
        {
            // Completar el UPN solo si ninguna otra fila lo tiene (índice único).
            if (porOid.Upn is null && token.Upn is not null && porUpn is not null && porUpn.Id != porOid.Id)
            {
                return Conflicto(porUpn, MotivoUpnOtroOid);
            }

            return new(TipoDecisionProvisionamiento.Encontrado, porOid, CompletarUpn: porOid.Upn is null && token.Upn is not null);
        }

        if (porUpn is not null)
        {
            return OtroOid(porUpn, token)
                ? Conflicto(porUpn, MotivoUpnOtroOid)
                : new(TipoDecisionProvisionamiento.Encontrado, porUpn, CompletarOid: FaltaOid(porUpn, token));
        }

        if (porCorreo is not null)
        {
            if (OtroOid(porCorreo, token))
            {
                return Conflicto(porCorreo, MotivoCorreoOtroOid);
            }

            // Q1: precargado (p. ej. Fase 3) con un UPN distinto del que trae el token.
            if (porCorreo.Upn is not null && token.Upn is not null && porCorreo.Upn != token.Upn)
            {
                return Conflicto(porCorreo, MotivoCorreoOtroUpn);
            }

            return new(TipoDecisionProvisionamiento.Encontrado, porCorreo, FaltaOid(porCorreo, token),
                porCorreo.Upn is null && token.Upn is not null);
        }

        return new(TipoDecisionProvisionamiento.Crear);
    }

    private static bool OtroOid(UsuarioIdentidad u, IdentidadToken t) =>
        u.EntraObjectId is not null && t.EntraObjectId is not null && u.EntraObjectId != t.EntraObjectId;

    private static bool FaltaOid(UsuarioIdentidad u, IdentidadToken t) => u.EntraObjectId is null && t.EntraObjectId is not null;

    private static DecisionProvisionamiento Conflicto(UsuarioIdentidad u, string motivo) =>
        new(TipoDecisionProvisionamiento.Conflicto, u, Motivo: motivo);
}
