using App.Domain.Seguridad;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    private const string Tabla = "Usuario";

    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable(Tabla);
        b.HasKey(u => u.Id).HasName("PK_Usuario");

        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.Property(u => u.NombreMostrar).HasMaxLength(200).IsRequired();
        b.Property(u => u.EsSistema).ConDefault(false, "DF_Usuario_EsSistema");
        b.Property(u => u.Activo).ConDefault(true, "DF_Usuario_Activo");

        b.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_Usuario_Email");
        b.HasIndex(u => u.EntraObjectId).IsUnique()
            .HasDatabaseName("UX_Usuario_EntraObjectId")
            .HasFilter("[EntraObjectId] IS NOT NULL");

        b.HasOne(u => u.Empleado).WithMany()
            .HasForeignKey(u => u.EmpleadoId)
            .HasConstraintName("FK_Usuario_Empleado")
            .OnDelete(DeleteBehavior.Restrict);

        b.ConAuditoria(Tabla);

        b.HasData(new Usuario
        {
            Id = Usuario.IdSistema,
            Email = "sistema@profesiograma.local",
            NombreMostrar = "SISTEMA",
            EsSistema = true,
            Activo = true,
            CreadoPorId = Semilla.Sistema,
            FechaCreacion = Semilla.Fecha
        });
    }
}
