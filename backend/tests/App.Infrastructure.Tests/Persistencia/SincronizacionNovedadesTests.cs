using App.Application.Novedades.Sincronizacion;
using App.Domain.Catalogos;
using App.Domain.Novedades;
using App.Infrastructure.NovedadesExternas;
using App.Infrastructure.Persistencia.Novedades;
using App.Infrastructure.Tests.Consultas;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Tests.Persistencia;

/// <summary>
/// TAREA-23-3: lector del CSV de SharePoint, fuente simulada, traducción SQL de las consultas nuevas y comportamiento de
/// EF sin base (nueva, sin cambios, diff de días). Todo con datos FICTICIOS (A6): nunca un CSV real de la lista.
/// </summary>
public class SincronizacionNovedadesTests(ITestOutputHelper salida) : BaseSql(salida)
{
    // ------------------------------------------------------------------ lector CSV (ficticio)

    private const string Encabezado = "\"Título\",\"NOVEDAD_UID\",\"TIPO_APLICACION\",\"ESTADO_NOVEDAD\",\"DETALLE_JSON\",\"CORREO_PERSONA\",\"Creado por\",\"ID\"";

    private static string Fila(string codigo, string uid, string estado, string json, string id, string correo = "") =>
        $"\"{codigo}\",\"{uid}\",\"PERSONA\",\"{estado}\",\"{json.Replace("\"", "\"\"", StringComparison.Ordinal)}\",\"{correo}\",\"PERSONA QUE CREO (FICTICIA)\",\"{id}\"";

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Csv_ConYSinBomYListSchema_ComillasEscapadas_YSaltosDeLinea(bool bom, bool listSchema)
    {
        var json = "{\"NovedadUID\":\"x\",\n\"TipoNovedad\":\"PERMISO MEDICO\"}"; // salto de línea dentro del campo
        var texto = (bom ? "﻿" : "") + (listSchema ? "ListSchema={\"schemaXmlList\":[\"<Field Name=\\\"Title\\\" />\"]}\n" : "")
                    + Encabezado + "\r\n" + Fila("NOV-FICT-0001", "uid-1", "ACTIVA", json, "41", "ficticio@profesiograma.local") + "\r\n";

        var (filas, ilegibles) = LectorCsvSharePoint.Leer(texto);

        var f = Assert.Single(filas);
        Assert.Equal(0, ilegibles);
        Assert.Equal((41, "NOV-FICT-0001", "uid-1", "ACTIVA", json, "ficticio@profesiograma.local"),
            (f.LegacyId, f.Codigo, f.Uid, f.Estado, f.DetalleJson, f.CorreoPersona));
    }

    [Fact]
    public void Csv_FilaCorta_Ilegible_Y_TitleEnIngles_SinCorreo()
    {
        var texto = "\"Title\",\"NOVEDAD_UID\",\"ESTADO_NOVEDAD\",\"DETALLE_JSON\",\"ID\"\n"
                    + "\"NOV-FICT-0001\",\"uid-1\",\"ACTIVA\",\"{}\",\"1\"\n"
                    + "\"NOV-FICT-0002\",\"uid-2\",\"ACTIVA\"\n"
                    + "\n"
                    + "\"NOV-FICT-0003\",\"uid-3\",\"ANULADA\",\"{}\",\"no-numero\"\n";

        var (filas, ilegibles) = LectorCsvSharePoint.Leer(texto);

        Assert.Equal(1, ilegibles);
        Assert.Equal(["NOV-FICT-0001", "NOV-FICT-0003"], filas.Select(f => f.Codigo));
        Assert.Null(filas[1].LegacyId);
        Assert.All(filas, f => Assert.Null(f.CorreoPersona));
    }

    [Theory]
    [InlineData("\"Título\",\"NOVEDAD_UID\",\"ESTADO_NOVEDAD\",\"ID\"\n")] // sin DETALLE_JSON
    [InlineData("\"NOVEDAD_UID\",\"ESTADO_NOVEDAD\",\"DETALLE_JSON\",\"ID\"\n")] // sin Título/Title
    [InlineData("")]
    public void Csv_SinColumnasEsperadas_NoDisponible(string texto)
    {
        var ex = Assert.Throws<FuenteNovedadesNoDisponibleException>(() => LectorCsvSharePoint.Leer(texto));

        Assert.Equal(FuenteNovedadesNoDisponibleException.TituloNoDisponible, ex.Titulo);
    }

    [Fact]
    public async Task ArchivoLocal_SinRutaOInexistente_NoDisponible_SinLaRutaEnElMensaje()
    {
        var sinRuta = await Assert.ThrowsAsync<FuenteNovedadesNoDisponibleException>(
            () => new FuenteNovedadesArchivoLocal(null).LeerPermisosMedicosAsync(null, TestContext.Current.CancellationToken));
        var ruta = Path.Combine(Path.GetTempPath(), $"no-existe-{Guid.NewGuid():N}.csv");
        var inexistente = await Assert.ThrowsAsync<FuenteNovedadesNoDisponibleException>(
            () => new FuenteNovedadesArchivoLocal(ruta).LeerPermisosMedicosAsync(null, TestContext.Current.CancellationToken));

        Assert.Contains("RutaArchivo", sinRuta.Detalle);
        Assert.DoesNotContain(ruta, inexistente.Detalle);
    }

    // ------------------------------------------------------------------ fuente simulada

    [Fact]
    public void Simulada_Base_Categorias_YEscenariosDeterministas()
    {
        var resultados = FuenteNovedadesSimulada.Filas(FuenteNovedades.EscenarioBase).Select(MapeoPermisoMedico.Mapear).ToList();

        // La 6 (ZZ999) se mapea: la omite el servicio como sinEmpleado (no tiene fila ni está activa en la API).
        Assert.Equal([1, 2, 3, 6, 7, 8, 9], resultados.Select((r, i) => (r, i + 1)).Where(x => x.r.Mapeado is not null).Select(x => x.Item2));
        Assert.Equal("ZZ999", resultados[5].Mapeado!.Ekon);
        Assert.Equal(["tipoNoSoportado", "jsonInvalido", "otroOrigen"], resultados.Where(r => r.Omision is not null).Select(r => r.Omision!.Categoria));

        var fechas = FuenteNovedadesSimulada.Filas(FuenteNovedades.EscenarioFechas);
        var anuladas = FuenteNovedadesSimulada.Filas(FuenteNovedades.EscenarioAnuladas);
        var base2 = FuenteNovedadesSimulada.Filas(FuenteNovedades.EscenarioBase);
        Assert.Equal(Enumerable.Range(0, 10).Where(i => i != 2), Enumerable.Range(0, 10).Where(i => fechas[i] == base2[i]));
        Assert.Equal(3, MapeoPermisoMedico.Mapear(fechas[2]).Mapeado!.Dias.Count);
        Assert.Equal(FuenteNovedadesSimulada.Validas, anuladas.Select((f, i) => (f, i + 1)).Where(x => x.f.Estado == "ANULADA").Select(x => x.Item2));
        Assert.Equal(["NOV-SIMPM-0001", "NOV-SIMPM-0010"], [base2[0].Codigo, base2[9].Codigo]);
        Assert.Equal(2_000_000_001, base2[0].LegacyId);
    }

    [Fact]
    public void Simulada_CorreoDeLaFila7_EsElDeDev004DelSembrador()
    {
        var m = MapeoPermisoMedico.Mapear(FuenteNovedadesSimulada.Filas("base")[6]).Mapeado!;

        Assert.Equal(((string?)null, "empleado04.dev@profesiograma.local"), (m.Ekon, m.Correo));
    }

    // ------------------------------------------------------------------ ToQueryString

    [Fact]
    public void ExistentesPorUid_ConDias_SinSeguimiento()
    {
        var sql = Sql(SincronizacionNovedadesRepositorio.ConsultaExistentesPorUid(Db, [Guid.NewGuid(), Guid.NewGuid()]));

        Assert.Contains("[Uid] IN (", sql);
        Assert.Contains("[NovedadDia]", sql);
        Assert.Contains("[DetalleOrigenJson]", sql);
    }

    [Fact]
    public void Codigos_Y_LegacyIds_Y_Uids() =>
        Assert.All(new[]
        {
            Sql(SincronizacionNovedadesRepositorio.ConsultaCodigos(Db, ["NOV-SIMPM-0001"])),
            Sql(SincronizacionNovedadesRepositorio.ConsultaLegacyIds(Db, [2_000_000_001])),
            Sql(SincronizacionNovedadesRepositorio.ConsultaUidsPermisosMedicos(Db)),
        }, sql => Assert.Contains("FROM [dbo].[Novedad]", sql));

    [Fact]
    public void LegacyIds_Y_Uids_SoloOrigenPermisosMedicos()
    {
        Assert.Contains("[OrigenNovedadId] = CAST(2 AS tinyint)", Sql(SincronizacionNovedadesRepositorio.ConsultaLegacyIds(Db, [2_000_000_001])));
        Assert.Contains("[OrigenNovedadId] = CAST(2 AS tinyint)", Sql(SincronizacionNovedadesRepositorio.ConsultaUidsPermisosMedicos(Db)));
    }

    [Fact]
    public void EmpleadosPorCorreo_SinCedula()
    {
        var sql = Sql(SincronizacionNovedadesRepositorio.ConsultaEmpleadosPorCorreo(Db,
            ["empleado04.dev@profesiograma.local", "empleado05.dev@profesiograma.local"]));

        Assert.Contains("[CorreoEmpresa] IN (", sql);
        Assert.DoesNotContain("[Cedula]", sql);
    }

    [Fact]
    public void ParaSincronizar_ConSeguimientoYDias() =>
        Assert.Contains("[NovedadDia]", Sql(SincronizacionNovedadesRepositorio.ConsultaParaSincronizar(Db, [Guid.NewGuid()])));

    // ------------------------------------------------------------------ EF sin base: nueva, sin cambios y diff de días

    private static PermisoMedicoMapeado Mapeado(string estado = "ACTIVA", string escenario = "base") =>
        MapeoPermisoMedico.Mapear(FuenteNovedadesSimulada.Filas(escenario)[2] with { Estado = estado }).Mapeado!; // fila 3 (DEV003)

    [Fact]
    public void Nueva_R5_Sistema_FechaRegistro_Origen2_YDias()
    {
        var m = Mapeado(escenario: "fechas");

        var n = SincronizacionNovedadesRepositorio.Nueva(m, 3);

        Assert.Equal((1, m.FechaRegistroUtc!.Value, CatalogoIds.OrigenNovedad.PermisosMedicos, CatalogoIds.TipoNovedad.PermisoMedico, 3),
            (n.CreadoPorId, n.FechaCreacion, n.OrigenNovedadId, n.TipoNovedadId, n.EmpleadoId));
        Assert.Equal(("NOV-SIMPM-0003", 2_000_000_003, (string?)null, "PLANTA"), (n.Codigo, n.LegacyId, n.Observacion, n.OrigenArea));
        Assert.Equal(m.Dias, n.Dias.Select(d => d.Fecha));
        Assert.All(n.Dias, d => Assert.Equal(1, d.CreadoPorId));
    }

    [Fact]
    public void Asignar_SinCambios_NoMarcaNada_YConCambioDeFechas_DiffDeDias()
    {
        var m = Mapeado();
        var guardada = SincronizacionNovedadesRepositorio.Nueva(m, 3);
        guardada.Id = 7;
        guardada.RowVer = [0, 0, 0, 0, 0, 0, 0, 9];
        var dia = 0L;
        foreach (var d in guardada.Dias) { d.Id = ++dia; d.NovedadId = 7; }
        Db.Attach(guardada);

        SincronizacionNovedadesRepositorio.Asignar(guardada, m, 3); // mismos valores
        Db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Unchanged, Db.Entry(guardada).State); // sin UPDATE: RowVer y FechaModificacion iguales (A5)

        var tresDias = Mapeado(escenario: "fechas");
        SincronizacionNovedadesRepositorio.Asignar(guardada, tresDias, 3);
        Db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Modified, Db.Entry(guardada).State);
        var estados = Db.ChangeTracker.Entries<NovedadDia>().Select(e => e.State).ToList();
        Assert.Equal(1, estados.Count(s => s == EntityState.Unchanged));
        Assert.Equal(2, estados.Count(s => s == EntityState.Added));
    }
}
