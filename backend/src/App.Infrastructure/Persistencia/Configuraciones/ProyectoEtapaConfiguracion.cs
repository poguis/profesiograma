using App.Domain.Proyectos;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class ProyectoEtapaConfiguracion : IEntityTypeConfiguration<ProyectoEtapa>
{
    private const string Tabla = "ProyectoEtapa";

    public void Configure(EntityTypeBuilder<ProyectoEtapa> b)
    {
        b.ToTable(Tabla, t =>
        {
            t.HasCheckConstraint("CK_ProyectoEtapa_Fechas", "[FechaFin] >= [FechaInicio]");
            t.HasCheckConstraint("CK_ProyectoEtapa_Snapshot", "[SnapshotPersonal] IS NULL OR ISJSON([SnapshotPersonal]) = 1");
        });
        b.HasKey(e => e.Id).HasName("PK_ProyectoEtapa");

        b.Property(e => e.EtapaUid).HasDefaultValueSql("NEWID()", "DF_ProyectoEtapa_EtapaUid");
        b.Property(e => e.ActividadCodigo).HasMaxLength(20).IsUnicode(false);
        b.Property(e => e.SnapshotPersonal); // NVARCHAR(MAX)

        b.HasIndex(e => new { e.ProyectoId, e.Version }).IsUnique().HasDatabaseName("UQ_ProyectoEtapa_Version");
        b.HasIndex(e => e.EtapaUid).IsUnique().HasDatabaseName("UQ_ProyectoEtapa_Uid");
        b.HasIndex(e => e.LegacyId).IsUnique().HasDatabaseName("UX_ProyectoEtapa_LegacyId")
            .HasFilter("[LegacyId] IS NOT NULL");

        b.HasOne(e => e.Proyecto).WithMany(p => p.Etapas).HasForeignKey(e => e.ProyectoId)
            .HasConstraintName("FK_ProyectoEtapa_Proyecto").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.TipoMovimiento).WithMany().HasForeignKey(e => e.TipoMovimientoId)
            .HasConstraintName("FK_ProyectoEtapa_TipoMovimiento").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.EstadoProyecto).WithMany().HasForeignKey(e => e.EstadoProyectoId)
            .HasConstraintName("FK_ProyectoEtapa_Estado").OnDelete(DeleteBehavior.Restrict);

        b.ConAuditoria(Tabla);
    }
}
