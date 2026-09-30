using App.Domain.Proyectos;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class ProyectoPersonalConfiguracion : IEntityTypeConfiguration<ProyectoPersonal>
{
    private const string Tabla = "ProyectoPersonal";

    public void Configure(EntityTypeBuilder<ProyectoPersonal> b)
    {
        b.ToTable(Tabla, t =>
        {
            t.HasCheckConstraint("CK_ProyectoPersonal_Rol", "[RolAsignacionId] IN (1, 2)");
            t.HasCheckConstraint("CK_ProyectoPersonal_Fechas", "[FechaFin] >= [FechaInicio]");
            t.HasCheckConstraint("CK_ProyectoPersonal_TipoRegistro", "[TipoRegistro] IN ('JORNADA','DESCANSO')");
            t.HasCheckConstraint("CK_ProyectoPersonal_Jornada",
                "[RolAsignacionId] <> 1 OR ([JornadaId] IS NOT NULL AND [DiasTrabajo] > 0)");
            t.HasCheckConstraint("CK_ProyectoPersonal_Inicial", "[EsPrincipalInicial] = 0 OR [RolAsignacionId] = 1");
        });
        b.HasKey(p => p.Id).HasName("PK_ProyectoPersonal");

        // Soporte de la FK compuesta desde ProyectoAsignacionDia (J5).
        // Nota EF: ProyectoId y EmpleadoId quedan inmutables una vez guardada la fila.
        b.HasAlternateKey(p => new { p.Id, p.ProyectoId, p.EmpleadoId }).HasName("UQ_ProyectoPersonal_Clave");

        b.Property(p => p.CargoAsignado).HasMaxLength(200);
        b.Property(p => p.DiasDescanso).HasDefaultValue((byte)0, "DF_ProyectoPersonal_DiasDescanso");
        b.Property(p => p.EsPrincipalInicial).ConDefault(false, "DF_ProyectoPersonal_EsPrincipalInicial");
        b.Property(p => p.TipoRegistro).HasMaxLength(10).IsUnicode(false).IsRequired()
            .HasDefaultValue(ProyectoPersonal.TipoRegistroJornada, "DF_ProyectoPersonal_TipoRegistro");
        b.Property(p => p.Observacion).HasMaxLength(500);

        b.HasIndex(p => new { p.ProyectoId, p.RolAsignacionId, p.Numero }).IsUnique()
            .HasDatabaseName("UQ_ProyectoPersonal_Numero");
        b.HasIndex(p => p.EmpleadoId).HasDatabaseName("IX_ProyectoPersonal_Empleado")
            .IncludeProperties(p => new { p.ProyectoId, p.FechaInicio, p.FechaFin });
        b.HasIndex(p => p.LegacyId).IsUnique().HasDatabaseName("UX_ProyectoPersonal_LegacyId")
            .HasFilter("[LegacyId] IS NOT NULL");

        b.HasOne(p => p.Proyecto).WithMany(x => x.Personal).HasForeignKey(p => p.ProyectoId)
            .HasConstraintName("FK_ProyectoPersonal_Proyecto").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.RolAsignacion).WithMany().HasForeignKey(p => p.RolAsignacionId)
            .HasConstraintName("FK_ProyectoPersonal_Rol").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Empleado).WithMany().HasForeignKey(p => p.EmpleadoId)
            .HasConstraintName("FK_ProyectoPersonal_Empleado").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Jornada).WithMany().HasForeignKey(p => p.JornadaId)
            .HasConstraintName("FK_ProyectoPersonal_Jornada").OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProyectoPersonal>().WithMany().HasForeignKey(p => p.PrincipalRelacionadoId)
            .HasConstraintName("FK_ProyectoPersonal_Principal").OnDelete(DeleteBehavior.Restrict);

        b.ConAuditoria(Tabla);
    }
}
