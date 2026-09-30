using App.Domain.Novedades;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class NovedadDiaConfiguracion : IEntityTypeConfiguration<NovedadDia>
{
    private const string Tabla = "NovedadDia";

    public void Configure(EntityTypeBuilder<NovedadDia> b)
    {
        b.ToTable(Tabla);
        b.HasKey(d => d.Id).HasName("PK_NovedadDia");

        b.HasIndex(d => new { d.NovedadId, d.Fecha }).IsUnique().HasDatabaseName("UQ_NovedadDia");
        b.HasIndex(d => d.Fecha).HasDatabaseName("IX_NovedadDia_Fecha").IncludeProperties(d => new { d.NovedadId });

        // Única FK en cascada del modelo (quitar días de una novedad).
        b.HasOne(d => d.Novedad).WithMany(n => n.Dias).HasForeignKey(d => d.NovedadId)
            .HasConstraintName("FK_NovedadDia_Novedad").OnDelete(DeleteBehavior.Cascade);

        b.ConAuditoria(Tabla);
    }
}
