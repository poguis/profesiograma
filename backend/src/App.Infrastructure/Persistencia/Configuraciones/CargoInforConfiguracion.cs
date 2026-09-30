using App.Domain.Configuracion;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class CargoInforConfiguracion : IEntityTypeConfiguration<CargoInfor>
{
    private const string Tabla = "CargoInfor";

    public void Configure(EntityTypeBuilder<CargoInfor> b)
    {
        b.ToTable(Tabla);
        b.HasKey(c => c.Id).HasName("PK_CargoInfor");

        b.Property(c => c.Cargo).HasMaxLength(200).IsRequired();
        b.Property(c => c.CodigoInfor).HasMaxLength(20).IsUnicode(false);
        b.Property(c => c.Activo).ConDefault(true, "DF_CargoInfor_Activo");

        b.HasIndex(c => c.Cargo).IsUnique().HasDatabaseName("UQ_CargoInfor_Cargo");

        b.ConAuditoria(Tabla);

        // Valores fijos actuales de la app (Switch CARGO INFOR).
        b.HasData(
            Nuevo(1, "PARAMEDICO", "P0021"),
            Nuevo(2, "DESARROLLADOR DE SOFTWARE", "PPOOPP"), // [PENDIENTE] parece valor de prueba
            Nuevo(3, "SUPERVISOR SSA", "P00030"));
    }

    private static CargoInfor Nuevo(int id, string cargo, string codigo) => new()
    {
        Id = id,
        Cargo = cargo,
        CodigoInfor = codigo,
        Activo = true,
        CreadoPorId = Semilla.Sistema,
        FechaCreacion = Semilla.Fecha
    };
}
