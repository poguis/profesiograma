using App.Application.Seguridad;

namespace App.Application.Tests.Seguridad;

/// <summary>TAREA-7b-2 (P3): búsqueda oid → UPN → correo, completar y conflictos. Datos FICTICIOS.</summary>
public class ReglasProvisionamientoTests
{
    private static readonly Guid Oid = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid OtroOid = Guid.Parse("cccccccc-0000-0000-0000-000000000009");
    private static readonly IdentidadToken Token = new(Oid, "upn.prueba@sedemi.com", "persona.prueba@sedemi.com");

    [Fact]
    public void NoExiste_Crear()
    {
        Assert.Equal(TipoDecisionProvisionamiento.Crear, ReglasProvisionamiento.Decidir(Token, null, null, null).Tipo);
    }

    [Fact]
    public void PorOid_ConUpn_SinCompletar()
    {
        var fila = new UsuarioIdentidad(5, Oid, "upn.prueba@sedemi.com", true);

        var d = ReglasProvisionamiento.Decidir(Token, fila, null, null);

        Assert.Equal((TipoDecisionProvisionamiento.Encontrado, 5, false, false), (d.Tipo, d.Usuario!.Id, d.CompletarOid, d.CompletarUpn));
    }

    [Fact]
    public void PorOid_SinUpn_CompletaElUpn_YSiOtraFilaLoTiene_Conflicto()
    {
        var fila = new UsuarioIdentidad(5, Oid, null, true);

        var completa = ReglasProvisionamiento.Decidir(Token, fila, null, null);
        var conflicto = ReglasProvisionamiento.Decidir(Token, fila, new UsuarioIdentidad(6, null, "upn.prueba@sedemi.com", true), null);

        Assert.True(completa.CompletarUpn);
        Assert.Equal((TipoDecisionProvisionamiento.Conflicto, 6, ReglasProvisionamiento.MotivoUpnOtroOid),
            (conflicto.Tipo, conflicto.Usuario!.Id, conflicto.Motivo));
    }

    [Fact]
    public void PorUpn_SinOid_CompletaElOid_ConOtroOid_Conflicto()
    {
        var sinOid = ReglasProvisionamiento.Decidir(Token, null, new UsuarioIdentidad(5, null, "upn.prueba@sedemi.com", true), null);
        var otro = ReglasProvisionamiento.Decidir(Token, null, new UsuarioIdentidad(5, OtroOid, "upn.prueba@sedemi.com", true), null);

        Assert.Equal((TipoDecisionProvisionamiento.Encontrado, true, false), (sinOid.Tipo, sinOid.CompletarOid, sinOid.CompletarUpn));
        Assert.Equal((TipoDecisionProvisionamiento.Conflicto, ReglasProvisionamiento.MotivoUpnOtroOid), (otro.Tipo, otro.Motivo));
    }

    /// <summary>El Admin precargó por correo; en el primer ingreso el UPN (distinto) y el oid se completan.</summary>
    [Fact]
    public void PrecargadoPorCorreo_UpnDistinto_CompletaOidYUpn()
    {
        var d = ReglasProvisionamiento.Decidir(Token, null, null, new UsuarioIdentidad(5, null, null, true));

        Assert.Equal((TipoDecisionProvisionamiento.Encontrado, 5, true, true), (d.Tipo, d.Usuario!.Id, d.CompletarOid, d.CompletarUpn));
    }

    [Fact]
    public void PorCorreo_ConOtroOid_Conflicto()
    {
        var d = ReglasProvisionamiento.Decidir(Token, null, null, new UsuarioIdentidad(5, OtroOid, null, true));

        Assert.Equal((TipoDecisionProvisionamiento.Conflicto, ReglasProvisionamiento.MotivoCorreoOtroOid), (d.Tipo, d.Motivo));
    }

    /// <summary>Q1: precargado con un UPN guardado distinto del que trae el token.</summary>
    [Fact]
    public void PorCorreo_ConUpnGuardadoDistinto_Conflicto()
    {
        var d = ReglasProvisionamiento.Decidir(Token, null, null, new UsuarioIdentidad(5, null, "otro.upn@sedemi.com", true));

        Assert.Equal((TipoDecisionProvisionamiento.Conflicto, ReglasProvisionamiento.MotivoCorreoOtroUpn), (d.Tipo, d.Motivo));
    }

    [Fact]
    public void TokenSinUpn_NoCompletaNiCompara()
    {
        var sinUpn = Token with { Upn = null };

        var d = ReglasProvisionamiento.Decidir(sinUpn, null, null, new UsuarioIdentidad(5, null, "otro.upn@sedemi.com", true));

        Assert.Equal((TipoDecisionProvisionamiento.Encontrado, true, false), (d.Tipo, d.CompletarOid, d.CompletarUpn));
    }

    [Fact]
    public void Inactivo_SeEncuentraIgual_ElAltaDecideElAcceso()
    {
        var d = ReglasProvisionamiento.Decidir(Token, new UsuarioIdentidad(5, Oid, "upn.prueba@sedemi.com", false), null, null);

        Assert.Equal(TipoDecisionProvisionamiento.Encontrado, d.Tipo);
        Assert.False(d.Usuario!.Activo);
    }
}
