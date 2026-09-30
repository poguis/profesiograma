using App.Domain.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistencia.Configuraciones;

internal sealed class EstadoProyectoConfiguracion : CatalogoConfiguracion<EstadoProyecto>
{
    protected override string Tabla => "EstadoProyecto";

    protected override IEnumerable<EstadoProyecto> Semillas() =>
    [
        Semilla(new EstadoProyecto { EsVigente = true },  1, "ACTIVO",     "Activo",     1),
        Semilla(new EstadoProyecto { EsVigente = true },  2, "SUSPENDIDO", "Suspendido", 2),
        Semilla(new EstadoProyecto { EsVigente = false }, 3, "INACTIVO",   "Inactivo",   3), // existe en SharePoint; sin transición (C8)
        Semilla(new EstadoProyecto { EsVigente = false }, 4, "TERMINADO",  "Terminado",  4),
    ];
}

internal sealed class TipoMovimientoConfiguracion : CatalogoConfiguracion<TipoMovimiento>
{
    protected override string Tabla => "TipoMovimiento";

    protected override IEnumerable<TipoMovimiento> Semillas() =>
    [
        Semilla(new TipoMovimiento(), 1, "CREACION",               "Creación",                   1),
        Semilla(new TipoMovimiento(), 2, "ACTUALIZACION_PERSONAL", "Actualización de personal",  2),
        Semilla(new TipoMovimiento(), 3, "SUSPENSION",             "Suspensión",                 3),
        Semilla(new TipoMovimiento(), 4, "CIERRE",                 "Cierre",                     4),
        Semilla(new TipoMovimiento(), 5, "REACTIVACION",           "Reactivación",               5),
        Semilla(new TipoMovimiento(), 6, "CAMBIO_ACTIVIDAD",       "Cambio de actividad",        6),
        Semilla(new TipoMovimiento(), 7, "EDICION_CABECERA",       "Edición de datos generales", 7),
    ];
}

internal sealed class GrupoProyectoConfiguracion : CatalogoConfiguracion<GrupoProyecto>
{
    protected override string Tabla => "GrupoProyecto";

    protected override void ConfigurarTabla(TableBuilder<GrupoProyecto> t)
        => t.HasCheckConstraint("CK_GrupoProyecto_Requisito", "[RequiereProyectoErp] = 1 OR [RequiereDimension] = 1");

    protected override IEnumerable<GrupoProyecto> Semillas() =>
    [
        Semilla(new GrupoProyecto { RequiereProyectoErp = true,  RequiereDimension = false }, 1, "CAMPO",                    "CAMPO",                    1),
        Semilla(new GrupoProyecto { RequiereProyectoErp = false, RequiereDimension = true },  2, "PLANTA",                   "PLANTA",                   2),
        Semilla(new GrupoProyecto { RequiereProyectoErp = false, RequiereDimension = true },  3, "OFICINAS ADMINISTRATIVAS", "OFICINAS ADMINISTRATIVAS", 3),
    ];
}

internal sealed class JornadaConfiguracion : CatalogoConfiguracion<Jornada>
{
    protected override string Tabla => "Jornada";

    protected override void ConfigurarTabla(TableBuilder<Jornada> t)
        => t.HasCheckConstraint("CK_Jornada_Dias", "[DiasTrabajo] > 0 AND [DiasDescanso] >= 0");

    protected override IEnumerable<Jornada> Semillas() =>
    [
        Semilla(new Jornada { DiasTrabajo = 22, DiasDescanso = 8 }, 1, "TIPO_1",   "Tipo 1 (22-8)",               1),
        Semilla(new Jornada { DiasTrabajo = 11, DiasDescanso = 4 }, 2, "TIPO_2",   "Tipo 2 (11-4)",               2),
        Semilla(new Jornada { DiasTrabajo = 5,  DiasDescanso = 2 }, 3, "TIPO_3",   "Tipo 3 (5-2)",                3),
        Semilla(new Jornada { DiasTrabajo = 3,  DiasDescanso = 0 }, 4, "ESPECIAL", "Especiales (3 días o menos)", 4),
    ];
}

internal sealed class RolAsignacionConfiguracion : CatalogoConfiguracion<RolAsignacion>
{
    protected override string Tabla => "RolAsignacion";

    protected override IEnumerable<RolAsignacion> Semillas() =>
    [
        Semilla(new RolAsignacion { EsDescanso = false }, 1, "PRINCIPAL", "Principal", 1),
        Semilla(new RolAsignacion { EsDescanso = false }, 2, "BACK",      "Back",      2),
        Semilla(new RolAsignacion { EsDescanso = true },  3, "DESCANSO",  "Descanso",  3),
    ];
}

internal sealed class TipoAplicacionNovedadConfiguracion : CatalogoConfiguracion<TipoAplicacionNovedad>
{
    protected override string Tabla => "TipoAplicacionNovedad";

    protected override IEnumerable<TipoAplicacionNovedad> Semillas() =>
    [
        Semilla(new TipoAplicacionNovedad(), 1, "PERSONA",  "Persona",  1),
        Semilla(new TipoAplicacionNovedad(), 2, "PROYECTO", "Proyecto", 2),
        Semilla(new TipoAplicacionNovedad(), 3, "GENERAL",  "General",  3),
    ];
}

internal sealed class OrigenNovedadConfiguracion : CatalogoConfiguracion<OrigenNovedad>
{
    protected override string Tabla => "OrigenNovedad";

    protected override IEnumerable<OrigenNovedad> Semillas() =>
    [
        Semilla(new OrigenNovedad { EsEditable = true },  1, "PROFESIOGRAMA",    "PROFESIOGRAMA",        1),
        Semilla(new OrigenNovedad { EsEditable = false }, 2, "PERMISOS_MEDICOS", "App Permisos Médicos", 2),
    ];
}

internal sealed class TipoNovedadConfiguracion : CatalogoConfiguracion<TipoNovedad>
{
    protected override string Tabla => "TipoNovedad";

    protected override void ConfigurarTabla(TableBuilder<TipoNovedad> t)
        => t.HasCheckConstraint("CK_TipoNovedad_ColorHex",
            "[ColorHex] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'");

    protected override void ConfigurarAdicional(EntityTypeBuilder<TipoNovedad> b)
    {
        b.Property(x => x.Sigla).HasMaxLength(10).IsUnicode(false).IsRequired();
        b.Property(x => x.SiglaCronograma).HasMaxLength(4).IsUnicode(false).IsRequired();
        b.Property(x => x.ColorHex).HasMaxLength(7).IsFixedLength().IsUnicode(false).IsRequired();
        b.Property(x => x.CategoriaDescripcion).HasMaxLength(100).IsRequired();
        b.Property(x => x.EstadoReporte).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.ObservacionReporte).HasMaxLength(60).IsRequired();
    }

    protected override IEnumerable<TipoNovedad> Semillas() =>
    [
        Semilla(Nuevo("CD",     "CD", "#8E44AD", "CALAMIDAD DOMESTICA", persona: true,  proyecto: false, general: false, manual: true),  1, "CALAMIDAD_DOMESTICA", "CALAMIDAD DOMESTICA", 1),
        Semilla(Nuevo("D",      "D",  "#607D8B", "DESCANSO",            persona: true,  proyecto: false, general: false, manual: false), 2, "DESCANSO",            "DESCANSO",            2),
        Semilla(Nuevo("FER",    "F",  "#D13438", "FERIADO",             persona: false, proyecto: true,  general: true,  manual: true),  3, "FERIADO",             "FERIADO",             3),
        Semilla(Nuevo("PTNDAD", "PT", "#27AE60", "PATERNIDAD",          persona: true,  proyecto: false, general: false, manual: true),  4, "PATERNIDAD",          "PATERNIDAD",          4),
        Semilla(Nuevo("P",      "P",  "#FFB900", "PERMISO",             persona: true,  proyecto: false, general: false, manual: true),  5, "PERMISO",             "PERMISO",             5),
        Semilla(Nuevo("V",      "V",  "#0277BD", "VACACIONES",          persona: true,  proyecto: false, general: false, manual: true),  6, "VACACIONES",          "VACACIONES",          6),
        Semilla(Nuevo("PM",     "PM", "#FFB900", "PERMISO MEDICO",      persona: true,  proyecto: false, general: false, manual: false), 7, "PERMISO_MEDICO",      "PERMISO MEDICO",      7),
    ];

    private static TipoNovedad Nuevo(string sigla, string siglaCrono, string color, string observacion,
        bool persona, bool proyecto, bool general, bool manual) => new()
    {
        Sigla = sigla,
        SiglaCronograma = siglaCrono,
        ColorHex = color,
        CategoriaId = 5,
        CategoriaDescripcion = "Asistencia Libre",
        EstadoReporte = "LIBRE",
        ObservacionReporte = observacion,
        AplicaPersona = persona,
        AplicaProyecto = proyecto,
        AplicaGeneral = general,
        SeleccionableManual = manual
    };
}
