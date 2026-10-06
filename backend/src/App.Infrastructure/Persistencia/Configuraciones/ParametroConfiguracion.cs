using App.Domain.Configuracion;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class ParametroConfiguracion : IEntityTypeConfiguration<Parametro>
{
    private const string Tabla = "Parametro";

    public void Configure(EntityTypeBuilder<Parametro> b)
    {
        b.ToTable(Tabla, t => t.HasCheckConstraint("CK_Parametro_TipoDato", "[TipoDato] IN ('INT','BOOL','TEXT','LIST')"));
        b.HasKey(p => p.Id).HasName("PK_Parametro");

        b.Property(p => p.Clave).HasMaxLength(60).IsUnicode(false).IsRequired();
        b.Property(p => p.Valor).HasMaxLength(400).IsRequired();
        b.Property(p => p.TipoDato).HasMaxLength(10).IsUnicode(false).IsRequired();
        b.Property(p => p.Descripcion).HasMaxLength(300);

        b.HasIndex(p => p.Clave).IsUnique().HasDatabaseName("UQ_Parametro_Clave");

        b.ConAuditoria(Tabla);

        b.HasData(
            Nuevo(1, "PROYECTO_MAX_PRINCIPALES", "20", "INT", "Máximo de principales por proyecto"),
            Nuevo(2, "PROYECTO_MAX_BACKS", "20", "INT", "Máximo de backs por proyecto"),
            Nuevo(3, "BACK_MAX_DIAS_DESCANSO", "20", "INT", "Máximo de días de descanso posteriores de un back"),
            Nuevo(4, "CRONOGRAMA_MAX_DIAS", "90", "INT", "Rango máximo del cronograma en días"),
            Nuevo(5, "PROYECTO_VISIBILIDAD", "CREADOR", "TEXT", "CREADOR = solo el propietario; DEPARTAMENTO = todo el departamento del proyecto"),
            Nuevo(6, "ALMUERZO_SALIDA_OPCIONES", "11:00,12:00,13:00,14:00", "LIST", "Horas permitidas de salida a almuerzo"),
            Nuevo(7, "ALMUERZO_REGRESO_OPCIONES", "12:00,13:00,14:00,15:00", "LIST", "Horas permitidas de regreso de almuerzo"),
            Nuevo(8, "EMPLEADO_FAMILIAS_ASIGNABLES", "ADMINISTRATIVO", "LIST", "Familias de puesto que se pueden asignar a proyectos"),
            Nuevo(9, "ZONA_HORARIA", "SA Pacific Standard Time", "TEXT", "Zona horaria de negocio (Ecuador, UTC-5)"),
            // TAREA-19y (pendiente 33): principal opcional; 1 vuelve a la regla C10.
            Nuevo(10, "PROYECTO_EXIGE_PRINCIPAL", "0", "BOOL",
                "1 = creación, actualización de personal y reactivación exigen al menos 1 principal (C10); 0 = basta 1 persona (principal o back)"));
    }

    private static Parametro Nuevo(int id, string clave, string valor, string tipo, string descripcion) => new()
    {
        Id = id,
        Clave = clave,
        Valor = valor,
        TipoDato = tipo,
        Descripcion = descripcion,
        CreadoPorId = Semilla.Sistema,
        FechaCreacion = Semilla.Fecha
    };
}
