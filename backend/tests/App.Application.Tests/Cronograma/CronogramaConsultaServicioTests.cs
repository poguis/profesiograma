using App.Application.Cronograma;
using App.Application.Seguridad;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Estados;
using static App.Application.Tests.Cronograma.DoblesCronograma;

namespace App.Application.Tests.Cronograma;

/// <summary>TAREA-20: rango (D5, D6, D12), validación, visibilidad R1 (D10) y "otros proyectos" (D2).</summary>
public class CronogramaConsultaServicioTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    /// <summary>19/10/2026 03:00 UTC = 18/10/2026 22:00 en Ecuador.</summary>
    private static readonly DateTimeOffset Ahora = new(2026, 10, 19, 3, 0, 0, TimeSpan.Zero);

    private static (CronogramaConsultaServicio Servicio, CronogramaConsultasFalsas Consultas) Crear(IUsuarioActual? usuario = null)
    {
        var consultas = new CronogramaConsultasFalsas();
        consultas.PropietarioDe[1] = 3; // del gestor (UsuarioFalso: 3)
        consultas.PropietarioDe[2] = 99; // de otro usuario
        return (new CronogramaConsultaServicio(consultas, usuario ?? new UsuarioFalso(), new RelojFijo(Ahora)), consultas);
    }

    private static CronogramaSolicitud S(string? desde = null, string? hasta = null, string? proyectoId = null,
        string? departamentoId = null, string? codigoEkon = null) => new(desde, hasta, proyectoId, departamentoId, codigoEkon);

    // ------------------------------------------------------------------ rango y validación

    [Fact]
    public async Task D6_SinFechas_ElMesActualEnEcuador()
    {
        var r = await Crear().Servicio.ConsultarAsync(S(), Ct);
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), (r.Valor!.Desde, r.Valor.Hasta));
    }

    [Fact]
    public async Task D6_SoloHasta_ElMesDeEsaFecha()
    {
        var r = await Crear().Servicio.ConsultarAsync(S(hasta: "2026-02-10"), Ct);
        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 10)), (r.Valor!.Desde, r.Valor.Hasta));
    }

    [Theory]
    [InlineData("01/10/2026", null, null, "desde", "La fecha 'desde' debe tener el formato yyyy-MM-dd.")]
    [InlineData(null, "x", null, "hasta", "La fecha 'hasta' debe tener el formato yyyy-MM-dd.")]
    [InlineData(null, null, "abc", "proyectoId", "El valor de 'proyectoId' debe ser un número entero mayor o igual a 1.")]
    [InlineData(null, null, "0", "proyectoId", "El valor de 'proyectoId' debe ser un número entero mayor o igual a 1.")]
    [InlineData("2026-10-10", "2026-10-09", null, "hasta", "La fecha 'hasta' no puede ser anterior a la fecha 'desde'.")]
    public async Task Invalido_400EnSuCampo(string? desde, string? hasta, string? proyectoId, string clave, string mensaje)
    {
        var r = await Crear().Servicio.ConsultarAsync(S(desde, hasta, proyectoId), Ct);
        Assert.Equal([mensaje], r.Errores![clave]);
    }

    [Fact]
    public async Task D5_Limite_90DiasValido_91Invalido()
    {
        var (servicio, _) = Crear();
        Assert.True((await servicio.ConsultarAsync(S("2026-10-01", "2026-12-29"), Ct)).EsValido);
        Assert.Equal(["El rango no puede superar 90 días."], (await servicio.ConsultarAsync(S("2026-10-01", "2026-12-30"), Ct)).Errores!["hasta"]);
    }

    [Fact]
    public async Task D5_UsaElParametro()
    {
        var (servicio, consultas) = Crear();
        consultas.MaxDias = 7;
        var r = await servicio.ConsultarAsync(S("2026-10-01", "2026-10-08"), Ct);
        Assert.Equal(["El rango no puede superar 7 días."], r.Errores!["hasta"]);

        consultas.MaxDias = null; // sin parámetro: 90
        Assert.Equal(90, (await servicio.ConsultarAsync(S("2026-10-01", "2026-10-08"), Ct)).Valor!.MaxDias);
    }

    [Fact]
    public async Task RangoSinDias_200Vacio_SinMasConsultas()
    {
        var (servicio, consultas) = Crear();
        var r = await servicio.ConsultarAsync(S(), Ct);

        Assert.Empty(r.Valor!.Personas);
        Assert.Equal(0, consultas.LecturasOtros);
    }

    // ------------------------------------------------------------------ visibilidad y filtros

    [Fact]
    public async Task Gestor_SoloSusProyectos_YCuentaLosDeOtros()
    {
        var (servicio, consultas) = Crear();
        consultas.Dias.AddRange([Dia(1, 1, 5), Dia(2, 2, 5)]);
        consultas.Otros.Add(new DiaOtrosProyectos(1, D(5), 1));

        var r = (await servicio.ConsultarAsync(S(), Ct)).Valor!;

        Assert.Equal(3, consultas.UltimoFiltro!.PropietarioUsuarioId);
        Assert.Equal((3, (int?)null), (consultas.UltimoFiltroOtros!.VisiblesDePropietario, consultas.UltimoFiltroOtros.SalvoProyectoId));
        Assert.Equal(["900001"], r.Personas.Select(p => p.CodigoEkon));
        Assert.Equal([1], r.Proyectos.Select(p => p.Id)); // D2: el proyecto ajeno no aparece
        Assert.Equal(("CHOQUE", 1), (Celda(r, 1, 5).Estado, Celda(r, 1, 5).OtrosProyectos));
    }

    [Fact]
    public async Task Admin_TodosLosProyectos_SinConsultaDeOtros()
    {
        var (servicio, consultas) = Crear(new AdminFalso());
        consultas.Dias.AddRange([Dia(1, 1, 5), Dia(1, 2, 5)]);

        var r = (await servicio.ConsultarAsync(S(), Ct)).Valor!;

        Assert.Null(consultas.UltimoFiltro!.PropietarioUsuarioId);
        Assert.Equal(0, consultas.LecturasOtros);
        Assert.Equal("CHOQUE", Celda(r, 1, 5).Estado);
    }

    [Fact]
    public async Task Filtros_LleganALaConsulta()
    {
        var (servicio, consultas) = Crear();
        await servicio.ConsultarAsync(S(proyectoId: "14", departamentoId: "7", codigoEkon: " 12345 "), Ct);

        var f = consultas.UltimoFiltro!;
        Assert.Equal((14, 7, "12345", (int?)null), (f.ProyectoId, f.DepartamentoId, f.CodigoEkon, f.SoloProyectoId));
    }

    [Fact]
    public async Task ProyectoSuspendidoAMitadDelRango_SoloSusDiasHastaF()
    {
        // El recorte de la suspensión (E3) borró los días > F: la consulta no los trae y no hay celdas después.
        var (servicio, consultas) = Crear();
        consultas.Dias.AddRange([Dia(1, 1, 10), Dia(1, 1, 15)]);

        var r = (await servicio.ConsultarAsync(S(), Ct)).Valor!;
        Assert.Equal([D(10), D(15)], r.Personas.Single().Dias.Select(d => d.Fecha));
    }

    // ------------------------------------------------------------------ por proyecto (D12)

    [Fact]
    public async Task PorProyecto_NoVisibleOInexistente_Null()
    {
        var (servicio, consultas) = Crear();
        consultas.Rangos[2] = new RangoProyecto(2, D(1), D(31));

        Assert.Null(await servicio.ConsultarProyectoAsync(2, new(null, null), Ct)); // de otro usuario
        Assert.Null(await servicio.ConsultarProyectoAsync(5, new(null, null), Ct));
    }

    [Fact]
    public async Task D12_SinFechas_DesdeElInicio_HastaElFin()
    {
        var (servicio, consultas) = Crear();
        consultas.Rangos[1] = new RangoProyecto(1, D(3), D(20));

        var r = (await servicio.ConsultarProyectoAsync(1, new(null, null), Ct))!.Valor!;
        Assert.Equal((D(3), D(20)), (r.Desde, r.Hasta));
        Assert.Equal(1, consultas.UltimoFiltro!.SoloProyectoId);
    }

    [Fact]
    public async Task D12_ProyectoLargo_Maximo90Dias()
    {
        var (servicio, consultas) = Crear();
        consultas.Rangos[1] = new RangoProyecto(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var r = (await servicio.ConsultarProyectoAsync(1, new(null, null), Ct))!.Valor!;
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)), (r.Desde, r.Hasta));
    }

    [Fact]
    public async Task D12_DesdeDespuesDelFin_90DiasDesdeEsaFecha()
    {
        var (servicio, consultas) = Crear();
        consultas.Rangos[1] = new RangoProyecto(1, D(3), D(20));

        var r = (await servicio.ConsultarProyectoAsync(1, new("2026-11-01", null), Ct))!.Valor!;
        Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2027, 1, 29)), (r.Desde, r.Hasta));
    }

    [Fact]
    public async Task PorProyecto_CuentaLosDemasProyectos_SinMostrarlos()
    {
        var (servicio, consultas) = Crear();
        consultas.Rangos[1] = new RangoProyecto(1, D(1), D(31));
        consultas.Dias.AddRange([Dia(1, 1, 5)]);
        consultas.Otros.Add(new DiaOtrosProyectos(1, D(5), 2));

        var r = (await servicio.ConsultarProyectoAsync(1, new(null, null), Ct))!.Valor!;

        Assert.Equal((int?)1, consultas.UltimoFiltroOtros!.SalvoProyectoId);
        Assert.Null(consultas.UltimoFiltroOtros.VisiblesDePropietario);
        Assert.Equal([1], r.Proyectos.Select(p => p.Id));
        Assert.Equal(("CHOQUE", 2), (Celda(r, 1, 5).Estado, Celda(r, 1, 5).OtrosProyectos));
    }

    [Fact]
    public async Task PorProyecto_FechaIlegible_400()
    {
        var (servicio, consultas) = Crear();
        consultas.Rangos[1] = new RangoProyecto(1, D(1), D(31));

        var r = await servicio.ConsultarProyectoAsync(1, new("ayer", null), Ct);
        Assert.Equal(["La fecha 'desde' debe tener el formato yyyy-MM-dd."], r!.Errores!["desde"]);
    }
}
