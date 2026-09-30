using App.Domain.Maestros;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class CompaniaConfiguracion : IEntityTypeConfiguration<Compania>
{
    private const string Tabla = "Compania";

    public void Configure(EntityTypeBuilder<Compania> b)
    {
        b.ToTable(Tabla);
        b.HasKey(c => c.Id).HasName("PK_Compania");
        b.Property(c => c.Id).ValueGeneratedNever(); // companyId del ERP

        b.Property(c => c.Nombre).HasMaxLength(300).IsRequired();
        b.Property(c => c.NombreComercial).HasMaxLength(300);
        b.Property(c => c.Ruc).HasMaxLength(13).IsUnicode(false);

        b.ConAuditoria(Tabla);
    }
}
