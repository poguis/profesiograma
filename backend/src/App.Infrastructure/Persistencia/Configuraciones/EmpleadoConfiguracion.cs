using App.Domain.Maestros;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class EmpleadoConfiguracion : IEntityTypeConfiguration<Empleado>
{
    private const string Tabla = "Empleado";

    public void Configure(EntityTypeBuilder<Empleado> b)
    {
        b.ToTable(Tabla);
        b.HasKey(e => e.Id).HasName("PK_Empleado");

        b.Property(e => e.CodigoEkon).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(e => e.Cedula).HasMaxLength(20).IsUnicode(false);
        b.Property(e => e.NombreCompleto).HasMaxLength(200).IsRequired();
        b.Property(e => e.Apellidos).HasMaxLength(120);
        b.Property(e => e.Nombres).HasMaxLength(120);
        b.Property(e => e.CorreoEmpresa).HasMaxLength(256);
        b.Property(e => e.CodEmpresa).HasMaxLength(10).IsUnicode(false);
        b.Property(e => e.Empresa).HasMaxLength(200);
        b.Property(e => e.CodPuesto).HasMaxLength(10).IsUnicode(false);
        b.Property(e => e.Puesto).HasMaxLength(200);
        b.Property(e => e.CodDepartamento).HasMaxLength(10).IsUnicode(false);
        b.Property(e => e.Departamento).HasMaxLength(200);
        b.Property(e => e.CodUnidad).HasMaxLength(10).IsUnicode(false);
        b.Property(e => e.Unidad).HasMaxLength(200);
        b.Property(e => e.CodArea).HasMaxLength(10).IsUnicode(false);
        b.Property(e => e.Area).HasMaxLength(200);
        b.Property(e => e.CodSeccion).HasMaxLength(10).IsUnicode(false);
        b.Property(e => e.Seccion).HasMaxLength(200);
        b.Property(e => e.FamiliaPuesto).HasMaxLength(100);
        // TAREA-26d: EstadoErp = estado en la API en la última alta puntual; FechaSincronizacion = última copia desde la API.
        b.Property(e => e.EstadoErp).HasMaxLength(1).IsFixedLength().IsUnicode(false);
        b.Property(e => e.EsOrigenLegado).ConDefault(false, "DF_Empleado_EsOrigenLegado");

        b.HasIndex(e => e.CodigoEkon).IsUnique().HasDatabaseName("UQ_Empleado_CodigoEkon");
        b.HasIndex(e => e.Cedula).HasDatabaseName("IX_Empleado_Cedula");
        b.HasIndex(e => e.CorreoEmpresa).HasDatabaseName("IX_Empleado_CorreoEmpresa");
        b.HasIndex(e => new { e.EstadoErp, e.FamiliaPuesto })
            .HasDatabaseName("IX_Empleado_Busqueda")
            .IncludeProperties(e => new { e.NombreCompleto, e.CodigoEkon, e.Departamento, e.Unidad, e.Puesto });

        b.ConAuditoria(Tabla);
    }
}
