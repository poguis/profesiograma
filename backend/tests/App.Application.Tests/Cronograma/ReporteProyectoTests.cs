using App.Application.Cronograma;
using App.Application.Cronograma.Reporte;
using App.Application.Proyectos;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Estados;
using App.Domain.Catalogos;
using static App.Application.Tests.Cronograma.DoblesCronograma;

namespace App.Application.Tests.Cronograma;

/// <summary>
/// TAREA-22-2: filas del reporte Excel (ArmadoReporte, Z4–Z7, D14, D16, supuestos 53 y 54) y el servicio (400, 404, rango
/// intersecado, resumen). Datos FICTICIOS: proyecto 1 (07:00–18:00, almuerzo 13:00–14:00, CAMPO, compañía 9001), cédulas "00…".
/// </summary>
public class ReporteProyectoTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const byte Descanso = CatalogoIds.RolAsignacion.Descanso;

    private static EmpleadoReporte Emp(int id, string? cedula = "0000000001", string? infor = "P0021") =>
        new(id, $"9000{id:00}", $"PERSONA {id:00}", cedula, infor);

    private static (IReadOnlyList<FilaReporte> Filas, ResumenReporteDto Resumen) Armar(
        IReadOnlyList<DiaCronograma> dias, IReadOnlyList<NovedadDiaCronograma>? novedades = null,
        IReadOnlyList<ActividadCronograma>? actividades = null, IReadOnlyList<EmpleadoReporte>? empleados = null,
        IReadOnlyList<DiaOtrosProyectos>? otros = null, ProyectoCronogramaDto? proyecto = null) =>
        ArmadoReporte.Armar(proyecto ?? Proyecto(1), dias, novedades ?? [], actividades ?? [Actividad(1, "20", 1, 31)],
            empleados ?? [Emp(1), Emp(2)], otros ?? []);

    private static ActividadCronograma Actividad(int version, string codigo, int desde, int hasta) =>
        new(1, version, version, codigo, D(desde), D(hasta));

    // ------------------------------------------------------------------ columnas por caso

    [Fact]
    public void Laborable_HorasDelProyecto_GrupoYDatosDeLaPersona()
    {
        var f = Assert.Single(Armar([Dia(1, 1, 2)]).Filas);

        Assert.Equal(new FilaReporte("9001", "900001", "0000000001", "PERSONA 01", "2/10/2026", new TimeSpan(7, 0, 0),
            new TimeSpan(14, 0, 0), new TimeSpan(13, 0, 0), new TimeSpan(18, 0, 0), "ERP-1", "20", "LABORABLE", "CAMPO", "P0021"), f);
    }

    [Fact]
    public void LibrePorNovedad_TextosDelTipo_Y4HorasEnCero()
    {
        var f = Assert.Single(Armar([Dia(1, 1, 2)], [Persona(7, 1, 2)]).Filas);

        Assert.Equal(("LIBRE", "VACACIONES"), (f.Estado, f.Observacion));
        Assert.All(new[] { f.IngresoJornada, f.IngresoAlmuerzo, f.SalidaAlmuerzo, f.SalidaJornada }, h => Assert.Equal(TimeSpan.Zero, h));
    }

    [Fact]
    public void LibrePorDescanso_Descanso_Y4HorasEnCero()
    {
        var f = Assert.Single(Armar([Dia(1, 1, 2, Descanso)]).Filas);

        Assert.Equal(("LIBRE", "DESCANSO", TimeSpan.Zero, TimeSpan.Zero), (f.Estado, f.Observacion, f.IngresoJornada, f.SalidaJornada));
    }

    [Fact]
    public void AlmuerzoNulo_00_Nocturno_TalCual_Y_PlantaSinProjectId_Vacio()
    {
        var nocturno = Proyecto(1) with
        {
            Grupo = "PLANTA",
            ProyectoErpId = null,
            Horario = ArmadoCronograma.Horario(73, "17:00 - 04:00", new TimeOnly(17, 0), new TimeOnly(4, 0)),
            Almuerzo = new AlmuerzoDto(null, null),
        };

        var f = Assert.Single(Armar([Dia(1, 1, 2)], proyecto: nocturno).Filas);

        Assert.Equal((new TimeSpan(17, 0, 0), TimeSpan.Zero, TimeSpan.Zero, new TimeSpan(4, 0, 0)), // D16 y supuesto 54
            (f.IngresoJornada, f.IngresoAlmuerzo, f.SalidaAlmuerzo, f.SalidaJornada));
        Assert.Null(f.Proyecto); // supuesto 53
        Assert.Equal("PLANTA", f.Observacion);
    }

    [Fact]
    public void SinCedula_SinCodigoInfor_SinActividad_Vacios_YContadosEnElResumen()
    {
        var r = Armar([Dia(1, 1, 2), Dia(1, 1, 3), Dia(2, 1, 2)], actividades: [], empleados: [Emp(1, cedula: null, infor: null), Emp(2)]);

        Assert.All(r.Filas.Where(f => f.Ekon == "900001"), f => Assert.Equal((null, null, (string?)null), (f.Cedula, f.CargoInfor, f.Actividad)));
        Assert.Equal(new ResumenReporteDto(Filas: 3, Personas: 2, PersonasSinCedula: 1, PersonasSinCodigoInfor: 1, FilasSinActividad: 3,
            DiasConOtroProyecto: 0), r.Resumen);
    }

    [Fact]
    public void Actividad_LaVigenteEseDia_SiNoHay_LaDeMayorVersion()
    {
        var r = Armar([Dia(1, 1, 2), Dia(1, 1, 10)], actividades: [Actividad(1, "20", 1, 5), Actividad(2, "21", 20, 31)]);

        Assert.Equal(["20", "21"], r.Filas.Select(f => f.Actividad));
    }

    // ------------------------------------------------------------------ precedencia y filas

    [Fact]
    public void Precedencia_Persona_General_Proyecto_IdMayor_YNovedadDeOtroProyectoNoCuenta()
    {
        var r = Armar([Dia(1, 1, 2), Dia(1, 1, 3), Dia(1, 1, 4), Dia(1, 1, 5)],
        [
            Persona(5, 1, 2, "PERMISO"), Persona(9, 1, 2, "VACACIONES"), General(20, 2),          // día 2: PERSONA de Id mayor
            General(3, 3), General(4, 3), DeProyecto(30, 1, 3),                                    // día 3: GENERAL de Id mayor
            DeProyecto(40, 1, 4), DeProyecto(41, 2, 4),                                            // día 4: PROYECTO de este proyecto
            DeProyecto(50, 2, 5),                                                                  // día 5: solo de otro proyecto
        ]);

        Assert.Equal(["VACACIONES", "FERIADO", "FERIADO", "CAMPO"], r.Filas.Select(f => f.Observacion));
        Assert.Equal(["LIBRE", "LIBRE", "LIBRE", "LABORABLE"], r.Filas.Select(f => f.Estado));
    }

    [Fact]
    public void UnaFilaPorPersonaYDia_ElTrabajoGanaAlDescanso_NovedadSinAsignacionSinFila()
    {
        var r = Armar([Dia(1, 1, 2, Descanso), Dia(1, 1, 2, CatalogoIds.RolAsignacion.Back)], [General(1, 9)]);

        var f = Assert.Single(r.Filas);
        Assert.Equal(("2/10/2026", "LABORABLE"), (f.Fecha, f.Estado));
    }

    [Fact]
    public void Orden_NombreEsEcSinMayusculas_LuegoFecha_YDiasConOtroProyecto()
    {
        var empleados = new[] { new EmpleadoReporte(1, "900001", "Ñandú Pérez", null, null), new EmpleadoReporte(2, "900002", "nube", null, null) };

        var r = Armar([Dia(1, 1, 3), Dia(2, 1, 2), Dia(1, 1, 2)], empleados: empleados, otros: [new DiaOtrosProyectos(1, D(3), 1)]);

        Assert.Equal(["900002 2/10/2026", "900001 2/10/2026", "900001 3/10/2026"], r.Filas.Select(f => $"{f.Ekon} {f.Fecha}"));
        Assert.Equal(1, r.Resumen.DiasConOtroProyecto);
    }

    // ------------------------------------------------------------------ Z7, fecha, nombre y ToString

    [Theory]
    [InlineData("9001", true)]
    [InlineData("7255", true)]
    [InlineData("120260053", true)]
    [InlineData("999999999999999", true)]   // 15 dígitos
    [InlineData("1000000000000000", false)] // 16 dígitos
    [InlineData("0123", false)]             // cero inicial
    [InlineData("0", false)]
    [InlineData("DEV001", false)]
    [InlineData("12A", false)]
    [InlineData("1 2", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Z7_NumeroSoloConDigitos_SinCeroInicial_Y15DigitosOMenos(string? texto, bool numero) =>
        Assert.Equal(numero, ArmadoReporte.EsNumeroExportable(texto, out _));

    [Fact]
    public void Fecha_SinCerosALaIzquierda_YNombreDeArchivoSaneado()
    {
        Assert.Equal("2/10/2026", ArmadoReporte.Fecha(new DateOnly(2026, 10, 2)));
        Assert.Equal("Reporte_PRY-2026_1_a_b_20261001_20261031.xlsx",
            ArmadoReporte.NombreArchivo("PRY-2026/1 a:b", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void ToString_SinLaCedula()
    {
        var f = Assert.Single(Armar([Dia(1, 1, 2)], empleados: [Emp(1, cedula: "0000000077")]).Filas);

        Assert.DoesNotContain("0000000077", f.ToString());
        Assert.DoesNotContain("0000000077", Emp(1, cedula: "0000000077").ToString());
    }

    // ------------------------------------------------------------------ servicio

    private static (ReporteProyectoServicio Servicio, CronogramaConsultasFalsas Consultas) Servicio(bool admin = false)
    {
        var consultas = new CronogramaConsultasFalsas();
        consultas.PropietarioDe[1] = 3; // del gestor (UsuarioFalso: 3)
        consultas.PropietarioDe[2] = 99;
        consultas.Rangos[1] = new RangoProyecto(1, D(5), D(20));
        consultas.Rangos[2] = new RangoProyecto(2, D(1), D(31));
        consultas.EmpleadosReporte.AddRange([Emp(1), Emp(2, cedula: null)]);
        return (new ReporteProyectoServicio(consultas, admin ? new AdminFalso() : new UsuarioFalso()), consultas);
    }

    [Theory]
    [InlineData(null, "2026-10-31", "desde", "La fecha 'desde' es obligatoria.")]
    [InlineData("2026-10-01", " ", "hasta", "La fecha 'hasta' es obligatoria.")]
    [InlineData("01/10/2026", "2026-10-31", "desde", "La fecha 'desde' debe tener el formato yyyy-MM-dd.")]
    [InlineData("2026-10-01", "31-10-2026", "hasta", "La fecha 'hasta' debe tener el formato yyyy-MM-dd.")]
    [InlineData("2026-10-31", "2026-10-01", "hasta", "La fecha 'hasta' no puede ser anterior a la fecha 'desde'.")]
    public async Task Servicio_400EnSuCampo(string? desde, string? hasta, string campo, string mensaje)
    {
        var r = await Servicio().Servicio.ArmarAsync(1, new ReporteProyectoSolicitud(desde, hasta), Ct);

        Assert.Equal([mensaje], r!.Errores![campo]);
    }

    [Fact]
    public async Task Servicio_404_InexistenteOEliminado_YNoVisibleParaElGestor_ElAdminSiLoVe()
    {
        var s = new ReporteProyectoSolicitud("2026-10-01", "2026-10-31");

        Assert.Null(await Servicio().Servicio.ArmarAsync(7, s, Ct));  // sin rango = inexistente o eliminado (consulta 5)
        Assert.Null(await Servicio().Servicio.ArmarAsync(2, s, Ct));  // de otro propietario
        Assert.Null(await Servicio().Servicio.ResumirAsync(2, s, Ct));
        Assert.NotNull(await Servicio(admin: true).Servicio.ArmarAsync(2, s, Ct));
    }

    [Fact]
    public async Task Servicio_SinTope_IntersectaElRango_NombreConLasFechasPedidas_YResumen()
    {
        var (servicio, consultas) = Servicio();
        consultas.Dias.AddRange([Dia(1, 1, 5), Dia(1, 1, 6), Dia(2, 1, 6), Dia(1, 2, 6)]);
        consultas.Otros.Add(new DiaOtrosProyectos(1, D(6), 1));

        var r = (await servicio.ArmarAsync(1, new ReporteProyectoSolicitud("2024-01-01", "2027-12-31"), Ct))!.Valor!;
        var resumen = (await servicio.ResumirAsync(1, new ReporteProyectoSolicitud("2024-01-01", "2027-12-31"), Ct))!.Valor!;

        Assert.Equal((D(5), D(20), 1), (consultas.UltimoFiltro!.Desde, consultas.UltimoFiltro.Hasta, consultas.UltimoFiltro.SoloProyectoId));
        Assert.Equal((1, D(5), D(20)), (consultas.UltimoFiltroOtros!.SalvoProyectoId, consultas.UltimoFiltroOtros.Desde, consultas.UltimoFiltroOtros.Hasta));
        Assert.Equal("Reporte_PRY-1_20240101_20271231.xlsx", r.NombreArchivo);
        Assert.Equal(3, r.Filas.Count); // el día del proyecto 2 no entra
        Assert.Equal(new ResumenReporteDto(3, 2, 1, 0, 3, 1), resumen); // el doble no trae actividades: 3 sin actividad
    }

    [Fact]
    public async Task Servicio_RangoFueraDelProyecto_SinFilas_NiConsultaDeDias()
    {
        var (servicio, consultas) = Servicio();

        var r = (await servicio.ArmarAsync(1, new ReporteProyectoSolicitud("2026-11-01", "2026-11-30"), Ct))!.Valor!;

        Assert.Empty(r.Filas);
        Assert.Equal(new ResumenReporteDto(0, 0, 0, 0, 0, 0), r.Resumen);
        Assert.Null(consultas.UltimoFiltro);
    }
}
