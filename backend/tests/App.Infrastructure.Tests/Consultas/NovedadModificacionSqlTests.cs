using App.Application.Novedades;
using App.Domain.Catalogos;
using App.Domain.Novedades;
using App.Infrastructure.Persistencia.Novedades;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// TAREA-23-2: consultas nuevas o modificadas de la edición y la anulación (ToQueryString, sin conexión) y R1 sin base:
/// tras preparar la edición, FechaModificacion queda marcada, RowVer lleva el token del cliente como OriginalValue y los
/// días salen como Deleted/Added (no se ejecuta SaveChanges).
/// </summary>
public class NovedadModificacionSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    private static readonly DateOnly D1 = new(2032, 1, 1), D2 = new(2032, 1, 2), D3 = new(2032, 1, 3), D4 = new(2032, 1, 4);

    [Fact]
    public void ParaModificar_ConSeguimientoYDias()
    {
        var sql = Sql(NovedadRepositorio.ConsultaParaModificar(Db, 5));

        Assert.Contains("FROM [dbo].[Novedad]", sql);
        Assert.Contains("[NovedadDia]", sql);
        Assert.Contains("[RowVer]", sql);
    }

    [Fact]
    public void DatosModificacion_Gestor_ConVisibilidad_Y_Admin_Sin()
    {
        var gestor = Sql(NovedadRepositorio.ConsultaDatosModificacion(Db, 5, 3));
        var admin = Sql(NovedadRepositorio.ConsultaDatosModificacion(Db, 5, null));

        Assert.Contains("[OrigenNovedadId] <> CAST(2 AS tinyint)", gestor);
        Assert.Contains("[EsEditable]", gestor);
        Assert.Contains("[PropietarioUsuarioId]", gestor);
        Assert.Contains("[Fecha]", gestor);
        Assert.DoesNotContain("[CreadoPorId] = @", admin);
        Assert.DoesNotContain("[Cedula]", admin);
        Assert.DoesNotContain("[DetalleOrigenJson]", admin);
    }

    [Fact]
    public void Duplicado_Edicion_ConDiasResultantesYSinLaPropia()
    {
        var dias = Enumerable.Range(0, 366).Select(i => D1.AddDays(i)).ToList(); // el máximo de días posible
        var sql = Sql(NovedadRepositorio.ConsultaDuplicado(Db, new CriterioDuplicado(
            CatalogoIds.TipoAplicacionNovedad.Persona, CatalogoIds.TipoNovedad.Vacaciones, 1, null, dias[0], dias[^1], dias, 5)));
        Assert.Contains("[Fecha] >= @", sql);
        // A3: EF 10 traduce Contains a IN con un parámetro por valor (relleno hasta 400 con 366 días): lejos del límite de
        // 2100 parámetros de SQL Server. No hace falta el plan B (cruzar los días en memoria).
        Assert.Contains("[Fecha] IN (@dias1, @dias2", sql);
        Assert.True(System.Text.RegularExpressions.Regex.Matches(sql, "DECLARE").Count < 2100);
        Assert.Contains("[Id] <> @", sql);
        Assert.Contains("[EmpleadoId] = @", sql);
    }

    [Fact]
    public void Duplicado_Creacion_SinCambios_SinListaNiExclusion()
    {
        var sql = Sql(NovedadRepositorio.ConsultaDuplicado(Db, new CriterioDuplicado(
            CatalogoIds.TipoAplicacionNovedad.General, CatalogoIds.TipoNovedad.Feriado, null, null, D1, D3)));

        Assert.DoesNotContain(" IN (", sql);
        Assert.DoesNotContain("[Id] <> @", sql);
    }

    [Fact]
    public void R1_PrepararEdicion_MarcaFechaModificacion_TokenComoOriginal_YDiffDeDias()
    {
        var guardado = new byte[] { 0, 0, 0, 0, 0, 0, 7, 209 };
        var token = new byte[] { 0, 0, 0, 0, 0, 0, 7, 200 };
        var novedad = new Novedad
        {
            Id = 5, Codigo = "NOV-20320101-abc123", TipoAplicacionNovedadId = CatalogoIds.TipoAplicacionNovedad.Persona,
            TipoNovedadId = CatalogoIds.TipoNovedad.Vacaciones, OrigenNovedadId = CatalogoIds.OrigenNovedad.Profesiograma,
            EmpleadoId = 1, FechaInicio = D1, FechaFin = D3, CreadoPorId = 3, RowVer = guardado,
            FechaModificacion = new DateTime(2032, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        foreach (var (id, fecha) in new[] { (11L, D1), (12L, D2), (13L, D3) })
        {
            novedad.Dias.Add(new NovedadDia { Id = id, NovedadId = 5, Fecha = fecha, CreadoPorId = 3 });
        }

        Db.Attach(novedad);

        // Mismo tipo y misma observación: solo cambian los días (sale D2, entra D4). Sin R1 no habría UPDATE de Novedad.
        NovedadRepositorio.PrepararEdicion(Db, novedad, new EdicionNovedad(5, token, CatalogoIds.TipoNovedad.Vacaciones, [D1, D3, D4], null));
        Db.ChangeTracker.DetectChanges();

        var entrada = Db.Entry(novedad);
        Assert.Equal(EntityState.Modified, entrada.State);
        Assert.True(entrada.Property(n => n.FechaModificacion).IsModified);
        Assert.Equal(token, entrada.Property(n => n.RowVer).OriginalValue);
        Assert.Equal((D1, D4), (novedad.FechaInicio, novedad.FechaFin));
        var estados = Db.ChangeTracker.Entries<NovedadDia>().ToDictionary(e => e.Entity.Fecha, e => e.State);
        Assert.Equal(EntityState.Unchanged, estados[D1]);
        Assert.Equal(EntityState.Deleted, estados[D2]);
        Assert.Equal(EntityState.Unchanged, estados[D3]);
        Assert.Equal(EntityState.Added, estados[D4]);
    }
}
