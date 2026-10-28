using App.Application.Empleados;
using App.Domain.Configuracion;
using App.Infrastructure.Consultas;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistencia.Empleados;

/// <summary>
/// TAREA-62: lectura y escritura del criterio de personal elegible (Parametro EMPLEADO_FAMILIAS_ASIGNABLES). La escritura
/// es un compare-and-set con ExecuteUpdate: WHERE Valor = valorAnterior (sin RowVer en Parametro, sin migración). Como
/// ExecuteUpdate no pasa por el AuditoriaInterceptor, ModificadoPorId y FechaModificacion se escriben aquí.
/// </summary>
internal sealed class CriterioPersonalRepositorio(ProfesiogramaDbContext db) : ICriterioPersonalRepositorio
{
    public async Task<CriterioGuardado> ObtenerAsync(CancellationToken ct)
    {
        var valor = await DatosReferenciaProyecto.ConsultaFamiliasAsignables(db).FirstOrDefaultAsync(ct);
        return new CriterioGuardado(valor, DatosReferenciaProyecto.SepararLista(valor));
    }

    public Task<int> ActualizarAsync(string valorAnterior, string valorNuevo, int usuarioId, DateTime fechaUtc, CancellationToken ct) =>
        ConsultaCriterioConValor(db, valorAnterior).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Valor, valorNuevo)
            .SetProperty(p => p.ModificadoPorId, usuarioId)
            .SetProperty(p => p.FechaModificacion, fechaUtc), ct);

    /// <summary>Fila del parámetro solo si su valor sigue siendo el que vio el Admin (prueba de traducción con ToQueryString).</summary>
    internal static IQueryable<Parametro> ConsultaCriterioConValor(ProfesiogramaDbContext db, string valorAnterior) =>
        db.Parametros.Where(p => p.Clave == ReglasPersonalElegible.ClaveParametro && p.Valor == valorAnterior);
}
