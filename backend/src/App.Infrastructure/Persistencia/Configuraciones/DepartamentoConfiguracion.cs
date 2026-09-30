using App.Domain.Seguridad;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class DepartamentoConfiguracion : IEntityTypeConfiguration<Departamento>
{
    private const string Tabla = "Departamento";

    public void Configure(EntityTypeBuilder<Departamento> b)
    {
        b.ToTable(Tabla, t => t.HasCheckConstraint("CK_Departamento_Tipo", "[Tipo] IN ('DEPARTAMENTO','UNIDAD')"));
        b.HasKey(d => d.Id).HasName("PK_Departamento");

        b.Property(d => d.Nombre).HasMaxLength(200).IsRequired();
        b.Property(d => d.NombreCorto).HasMaxLength(60).IsRequired();
        b.Property(d => d.Tipo).HasMaxLength(12).IsUnicode(false).IsRequired();
        b.Property(d => d.CodigoErp).HasMaxLength(10).IsUnicode(false);
        b.Property(d => d.Orden).HasDefaultValue((short)0, "DF_Departamento_Orden");
        b.Property(d => d.Activo).ConDefault(true, "DF_Departamento_Activo");

        b.HasIndex(d => d.Nombre).IsUnique().HasDatabaseName("UQ_Departamento_Nombre");

        b.ConAuditoria(Tabla);

        b.HasData(
            Nuevo(1, "DEPARTAMENTO DE INFRAESTRUCTURA", "Infraestructura Integral", Departamento.TipoDepartamento),
            Nuevo(2, "DEPARTAMENTO SEDEMI TELECOM", "Telecomunicaciones", Departamento.TipoDepartamento),
            Nuevo(3, "DEPARTAMENTO SEDEMI PETROLEO Y GAS", "Petróleo y Gas", Departamento.TipoDepartamento),
            Nuevo(4, "DEPARTAMENTO SEDEMI ENERGIA", "Energía", Departamento.TipoDepartamento),
            Nuevo(5, "DEPARTAMENTO DE INFRAESTRUCTURA METALICA", "Infraestructura Metálica", Departamento.TipoDepartamento),
            Nuevo(6, "DEPARTAMENTO SEDEMI MINERIA", "Minería", Departamento.TipoDepartamento),
            Nuevo(7, "UNIDAD SISTEMA INTEGRADO DE GESTION", "SIG", Departamento.TipoUnidad));
    }

    private static Departamento Nuevo(int id, string nombre, string corto, string tipo) => new()
    {
        Id = id,
        Nombre = nombre,
        NombreCorto = corto,
        Tipo = tipo,
        Orden = (short)id,
        Activo = true,
        CreadoPorId = Semilla.Sistema,
        FechaCreacion = Semilla.Fecha
    };
}
