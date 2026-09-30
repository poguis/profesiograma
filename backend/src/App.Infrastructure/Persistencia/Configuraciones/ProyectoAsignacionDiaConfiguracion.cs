using App.Domain.Proyectos;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class ProyectoAsignacionDiaConfiguracion : IEntityTypeConfiguration<ProyectoAsignacionDia>
{
    private const string Tabla = "ProyectoAsignacionDia";

    public void Configure(EntityTypeBuilder<ProyectoAsignacionDia> b)
    {
        b.ToTable(Tabla, t => t.HasCheckConstraint("CK_ProyectoAsignacionDia_Tipo", "[TipoAsignacion] IN ('AUTO','MANUAL')"));
        b.HasKey(d => d.Id).HasName("PK_ProyectoAsignacionDia");

        b.Property(d => d.TipoAsignacion).HasMaxLength(6).IsUnicode(false).IsRequired();

        // CLAVE original UID|EKON|fecha|ROL
        b.HasIndex(d => new { d.ProyectoId, d.EmpleadoId, d.Fecha, d.RolAsignacionId }).IsUnique()
            .HasDatabaseName("UQ_ProyectoAsignacionDia_Clave");
        b.HasIndex(d => new { d.EmpleadoId, d.Fecha }).HasDatabaseName("IX_ProyectoAsignacionDia_Cruces")
            .IncludeProperties(d => new { d.ProyectoId, d.RolAsignacionId });
        b.HasIndex(d => d.Fecha).HasDatabaseName("IX_ProyectoAsignacionDia_Cronograma")
            .IncludeProperties(d => new { d.ProyectoId, d.EmpleadoId, d.RolAsignacionId });
        b.HasIndex(d => d.ProyectoPersonalId).HasDatabaseName("IX_ProyectoAsignacionDia_Personal");
        b.HasIndex(d => d.LegacyId).IsUnique().HasDatabaseName("UX_ProyectoAsignacionDia_LegacyId")
            .HasFilter("[LegacyId] IS NOT NULL");

        // FK compuesta: garantiza que EmpleadoId/ProyectoId coincidan con la fila de ProyectoPersonal.
        b.HasOne(d => d.ProyectoPersonal).WithMany(p => p.AsignacionesDia)
            .HasForeignKey(d => new { d.ProyectoPersonalId, d.ProyectoId, d.EmpleadoId })
            .HasPrincipalKey(p => new { p.Id, p.ProyectoId, p.EmpleadoId })
            .HasConstraintName("FK_ProyectoAsignacionDia_Personal")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Proyecto>().WithMany().HasForeignKey(d => d.ProyectoId)
            .HasConstraintName("FK_ProyectoAsignacionDia_Proyecto").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(d => d.RolAsignacion).WithMany().HasForeignKey(d => d.RolAsignacionId)
            .HasConstraintName("FK_ProyectoAsignacionDia_Rol").OnDelete(DeleteBehavior.Restrict);

        b.ConAuditoria(Tabla);
    }
}
