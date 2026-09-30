using App.Domain.Seguridad;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class UsuarioDepartamentoConfiguracion : IEntityTypeConfiguration<UsuarioDepartamento>
{
    private const string Tabla = "UsuarioDepartamento";

    public void Configure(EntityTypeBuilder<UsuarioDepartamento> b)
    {
        b.ToTable(Tabla);
        b.HasKey(x => x.Id).HasName("PK_UsuarioDepartamento");

        b.Property(x => x.Notificado).ConDefault(false, "DF_UsuarioDepartamento_Notificado");
        b.Property(x => x.Activo).ConDefault(true, "DF_UsuarioDepartamento_Activo");

        b.HasIndex(x => new { x.UsuarioId, x.DepartamentoId }).IsUnique().HasDatabaseName("UQ_UsuarioDepartamento");

        b.HasOne(x => x.Usuario).WithMany(u => u.Departamentos)
            .HasForeignKey(x => x.UsuarioId)
            .HasConstraintName("FK_UsuarioDepartamento_Usuario")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Departamento).WithMany()
            .HasForeignKey(x => x.DepartamentoId)
            .HasConstraintName("FK_UsuarioDepartamento_Departamento")
            .OnDelete(DeleteBehavior.Restrict);

        b.ConAuditoria(Tabla);
    }
}
