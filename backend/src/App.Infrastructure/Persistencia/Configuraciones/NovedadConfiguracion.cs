using App.Domain.Novedades;
using App.Domain.Seguridad;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class NovedadConfiguracion : IEntityTypeConfiguration<Novedad>
{
    private const string Tabla = "Novedad";

    public void Configure(EntityTypeBuilder<Novedad> b)
    {
        b.ToTable(Tabla, t =>
        {
            t.HasCheckConstraint("CK_Novedad_Fechas", "[FechaFin] >= [FechaInicio]");
            t.HasCheckConstraint("CK_Novedad_Aplicacion",
                "([TipoAplicacionNovedadId] = 1 AND [EmpleadoId] IS NOT NULL AND [ProyectoId] IS NULL) " +
                "OR ([TipoAplicacionNovedadId] = 2 AND [ProyectoId] IS NOT NULL AND [EmpleadoId] IS NULL) " +
                "OR ([TipoAplicacionNovedadId] = 3 AND [ProyectoId] IS NULL AND [EmpleadoId] IS NULL)");
            t.HasCheckConstraint("CK_Novedad_Anulacion",
                "[Anulada] = 0 OR ([FechaAnulacion] IS NOT NULL AND [AnuladaPorId] IS NOT NULL)");
            t.HasCheckConstraint("CK_Novedad_DetalleJson", "[DetalleOrigenJson] IS NULL OR ISJSON([DetalleOrigenJson]) = 1");
        });
        b.HasKey(n => n.Id).HasName("PK_Novedad");

        b.Property(n => n.Uid).HasDefaultValueSql("NEWID()", "DF_Novedad_Uid");
        b.Property(n => n.Codigo).HasMaxLength(30).IsUnicode(false).IsRequired();
        b.Property(n => n.Observacion).HasMaxLength(500);
        b.Property(n => n.OrigenArea).HasMaxLength(30);
        b.Property(n => n.RegistradoPorNombre).HasMaxLength(200);
        b.Property(n => n.RegistradoPorCorreo).HasMaxLength(256);
        b.Property(n => n.DetalleOrigenJson); // NVARCHAR(MAX)
        b.Property(n => n.Anulada).ConDefault(false, "DF_Novedad_Anulada");
        b.Property(n => n.MotivoAnulacion).HasMaxLength(500);
        b.Property(n => n.RowVer).IsRowVersion();

        b.HasIndex(n => n.Uid).IsUnique().HasDatabaseName("UQ_Novedad_Uid");
        b.HasIndex(n => n.Codigo).IsUnique().HasDatabaseName("UQ_Novedad_Codigo");
        b.HasIndex(n => n.EmpleadoId).HasDatabaseName("IX_Novedad_Empleado")
            .HasFilter("[EmpleadoId] IS NOT NULL AND [Anulada] = 0");
        b.HasIndex(n => n.ProyectoId).HasDatabaseName("IX_Novedad_Proyecto")
            .HasFilter("[ProyectoId] IS NOT NULL AND [Anulada] = 0");
        b.HasIndex(n => new { n.FechaInicio, n.FechaFin }).HasDatabaseName("IX_Novedad_Rango")
            .IncludeProperties(n => new { n.TipoAplicacionNovedadId, n.TipoNovedadId, n.EmpleadoId, n.ProyectoId })
            .HasFilter("[Anulada] = 0");
        b.HasIndex(n => new { n.LegacyId, n.OrigenNovedadId }).IsUnique().HasDatabaseName("UX_Novedad_LegacyId")
            .HasFilter("[LegacyId] IS NOT NULL");

        b.HasOne(n => n.TipoAplicacionNovedad).WithMany().HasForeignKey(n => n.TipoAplicacionNovedadId)
            .HasConstraintName("FK_Novedad_TipoAplicacion").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(n => n.TipoNovedad).WithMany().HasForeignKey(n => n.TipoNovedadId)
            .HasConstraintName("FK_Novedad_TipoNovedad").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(n => n.OrigenNovedad).WithMany().HasForeignKey(n => n.OrigenNovedadId)
            .HasConstraintName("FK_Novedad_Origen").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(n => n.Empleado).WithMany().HasForeignKey(n => n.EmpleadoId)
            .HasConstraintName("FK_Novedad_Empleado").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(n => n.Proyecto).WithMany().HasForeignKey(n => n.ProyectoId)
            .HasConstraintName("FK_Novedad_Proyecto").OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany().HasForeignKey(n => n.AnuladaPorId)
            .HasConstraintName("FK_Novedad_AnuladaPor").OnDelete(DeleteBehavior.Restrict);

        b.ConAuditoria(Tabla);
    }
}
