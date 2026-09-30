using App.Domain.Proyectos;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class ProyectoConfiguracion : IEntityTypeConfiguration<Proyecto>
{
    private const string Tabla = "Proyecto";

    public void Configure(EntityTypeBuilder<Proyecto> b)
    {
        b.ToTable(Tabla, t =>
        {
            t.HasCheckConstraint("CK_Proyecto_Fechas", "[FechaFin] >= [FechaInicio]");
            t.HasCheckConstraint("CK_Proyecto_Almuerzo",
                "[SalidaAlmuerzo] IS NULL OR [RegresoAlmuerzo] IS NULL OR [RegresoAlmuerzo] > [SalidaAlmuerzo]");
            t.HasCheckConstraint("CK_Proyecto_Origen", "[ProyectoErpId] IS NOT NULL OR [DimensionUegpId] IS NOT NULL");
        });
        b.HasKey(p => p.Id).HasName("PK_Proyecto");

        b.Property(p => p.Uid).HasDefaultValueSql("NEWID()", "DF_Proyecto_Uid");
        b.Property(p => p.Codigo).HasMaxLength(30).IsUnicode(false).IsRequired();
        b.Property(p => p.NombreVisual).HasMaxLength(300).IsRequired();
        b.Property(p => p.ProyectoErpId).HasMaxLength(30).IsUnicode(false);
        b.Property(p => p.ProyectoErpNombre).HasMaxLength(300);
        b.Property(p => p.ProyectoErpEstado).HasMaxLength(30);
        b.Property(p => p.DimensionUegpId).HasMaxLength(30).IsUnicode(false);
        b.Property(p => p.DimensionDescripcion).HasMaxLength(300);
        b.Property(p => p.HorarioDescripcion).HasMaxLength(200);
        b.Property(p => p.TipoHorario).HasMaxLength(5).IsUnicode(false);
        b.Property(p => p.Eliminado).ConDefault(false, "DF_Proyecto_Eliminado");
        b.Property(p => p.RowVer).IsRowVersion();

        // Únicos
        b.HasIndex(p => p.Uid).IsUnique().HasDatabaseName("UQ_Proyecto_Uid");
        b.HasIndex(p => p.Codigo).IsUnique().HasDatabaseName("UQ_Proyecto_Codigo");
        b.HasIndex(p => p.LegacyId).IsUnique().HasDatabaseName("UX_Proyecto_LegacyId").HasFilter("[LegacyId] IS NOT NULL");

        // Consulta
        b.HasIndex(p => p.EstadoProyectoId).HasDatabaseName("IX_Proyecto_Estado")
            .IncludeProperties(p => new { p.Codigo, p.NombreVisual, p.FechaInicio, p.FechaFin })
            .HasFilter("[Eliminado] = 0");
        b.HasIndex(p => p.PropietarioUsuarioId).HasDatabaseName("IX_Proyecto_Propietario").HasFilter("[Eliminado] = 0");
        b.HasIndex(p => p.DepartamentoId).HasDatabaseName("IX_Proyecto_Departamento").HasFilter("[Eliminado] = 0");
        b.HasIndex(p => p.NombreVisual).HasDatabaseName("IX_Proyecto_Nombre");

        // Relaciones
        b.HasOne(p => p.GrupoProyecto).WithMany().HasForeignKey(p => p.GrupoProyectoId)
            .HasConstraintName("FK_Proyecto_GrupoProyecto").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Compania).WithMany().HasForeignKey(p => p.CompaniaId)
            .HasConstraintName("FK_Proyecto_Compania").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.EstadoProyecto).WithMany().HasForeignKey(p => p.EstadoProyectoId)
            .HasConstraintName("FK_Proyecto_EstadoProyecto").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Departamento).WithMany().HasForeignKey(p => p.DepartamentoId)
            .HasConstraintName("FK_Proyecto_Departamento").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Propietario).WithMany().HasForeignKey(p => p.PropietarioUsuarioId)
            .HasConstraintName("FK_Proyecto_Propietario").OnDelete(DeleteBehavior.Restrict);

        b.ConAuditoria(Tabla);
    }
}
