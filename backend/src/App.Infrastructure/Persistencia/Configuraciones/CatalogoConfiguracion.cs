using App.Domain.Comun;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Configuración común de catálogos: PK TINYINT sin identity, Codigo único,
/// Nombre, Orden, Activo (DEFAULT 1), auditoría y semillas.
/// </summary>
internal abstract class CatalogoConfiguracion<T> : IEntityTypeConfiguration<T> where T : Catalogo
{
    protected abstract string Tabla { get; }

    public void Configure(EntityTypeBuilder<T> b)
    {
        b.ToTable(Tabla, ConfigurarTabla);
        b.HasKey(c => c.Id).HasName($"PK_{Tabla}");
        b.Property(c => c.Id).ValueGeneratedNever();

        b.Property(c => c.Codigo).HasMaxLength(30).IsUnicode(false).IsRequired();
        b.Property(c => c.Nombre).HasMaxLength(100).IsRequired();
        b.Property(c => c.Activo).ConDefault(true, $"DF_{Tabla}_Activo");

        b.HasIndex(c => c.Codigo).IsUnique().HasDatabaseName($"UQ_{Tabla}_Codigo");

        b.ConAuditoria(Tabla);
        ConfigurarAdicional(b);
        b.HasData(Semillas());
    }

    protected virtual void ConfigurarTabla(TableBuilder<T> t) { }

    protected virtual void ConfigurarAdicional(EntityTypeBuilder<T> b) { }

    protected abstract IEnumerable<T> Semillas();

    /// <summary>Completa los campos comunes de una semilla.</summary>
    protected static T Semilla(T item, byte id, string codigo, string nombre, byte orden)
    {
        item.Id = id;
        item.Codigo = codigo;
        item.Nombre = nombre;
        item.Orden = orden;
        item.Activo = true;
        item.CreadoPorId = Convenciones.Semilla.Sistema;
        item.FechaCreacion = Convenciones.Semilla.Fecha;
        return item;
    }
}
