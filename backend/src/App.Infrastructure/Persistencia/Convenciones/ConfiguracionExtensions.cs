using App.Domain.Comun;
using App.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Convenciones;

internal static class ConfiguracionExtensions
{
    /// <summary>
    /// Columnas de auditoría + FK a Usuario con los nombres del script de Fase 2:
    /// DF_{Tabla}_FechaCreacion, FK_{Tabla}_CreadoPor, FK_{Tabla}_ModificadoPor.
    /// </summary>
    public static EntityTypeBuilder<T> ConAuditoria<T>(this EntityTypeBuilder<T> b, string tabla)
        where T : class, IAuditable
    {
        b.Property<int>(nameof(IAuditable.CreadoPorId)).IsRequired();
        b.Property<DateTime>(nameof(IAuditable.FechaCreacion))
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()", $"DF_{tabla}_FechaCreacion");
        b.Property<int?>(nameof(IAuditable.ModificadoPorId));
        b.Property<DateTime?>(nameof(IAuditable.FechaModificacion));

        b.HasOne<Usuario>().WithMany()
            .HasForeignKey(nameof(IAuditable.CreadoPorId))
            .HasConstraintName($"FK_{tabla}_CreadoPor")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Usuario>().WithMany()
            .HasForeignKey(nameof(IAuditable.ModificadoPorId))
            .HasConstraintName($"FK_{tabla}_ModificadoPor")
            .OnDelete(DeleteBehavior.Restrict);

        return b;
    }

    /// <summary>
    /// DEFAULT con nombre para BIT. Si el default es 1 se usa sentinel=true para que
    /// EF envíe explícitamente el valor false (evita que se pierda un Activo = false).
    /// </summary>
    public static PropertyBuilder<bool> ConDefault(this PropertyBuilder<bool> p, bool valor, string nombre)
        => valor
            ? p.HasDefaultValue(true, nombre).HasSentinel(true)
            : p.HasDefaultValue(false, nombre);
}
