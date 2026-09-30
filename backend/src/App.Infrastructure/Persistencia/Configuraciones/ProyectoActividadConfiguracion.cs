using App.Domain.Proyectos;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class ProyectoActividadConfiguracion : IEntityTypeConfiguration<ProyectoActividad>
{
    private const string Tabla = "ProyectoActividad";

    public void Configure(EntityTypeBuilder<ProyectoActividad> b)
    {
        b.ToTable(Tabla, t => t.HasCheckConstraint("CK_ProyectoActividad_Fechas", "[FechaFin] >= [FechaInicio]"));
        b.HasKey(a => a.Id).HasName("PK_ProyectoActividad");

        b.Property(a => a.MovimientoUid).HasDefaultValueSql("NEWID()", "DF_ProyectoActividad_Uid");
        b.Property(a => a.ActividadCodigo).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(a => a.ActividadDescripcion).HasMaxLength(300);
        b.Property(a => a.ActividadTipo).HasMaxLength(100);

        b.HasIndex(a => new { a.ProyectoId, a.Version }).IsUnique().HasDatabaseName("UQ_ProyectoActividad_Version");
        b.HasIndex(a => a.MovimientoUid).IsUnique().HasDatabaseName("UQ_ProyectoActividad_Uid");
        b.HasIndex(a => new { a.ProyectoId, a.FechaInicio, a.FechaFin }).HasDatabaseName("IX_ProyectoActividad_Vigencia")
            .IncludeProperties(a => new { a.ActividadCodigo, a.Version });
        b.HasIndex(a => a.LegacyId).IsUnique().HasDatabaseName("UX_ProyectoActividad_LegacyId")
            .HasFilter("[LegacyId] IS NOT NULL");

        b.HasOne(a => a.Proyecto).WithMany(p => p.Actividades).HasForeignKey(a => a.ProyectoId)
            .HasConstraintName("FK_ProyectoActividad_Proyecto").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.TipoMovimiento).WithMany().HasForeignKey(a => a.TipoMovimientoId)
            .HasConstraintName("FK_ProyectoActividad_TipoMovimiento").OnDelete(DeleteBehavior.Restrict);

        b.ConAuditoria(Tabla);
    }
}
