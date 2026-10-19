using App.Application.Comun;
using App.Application.Seguridad;

namespace App.Application.Cronograma;

/// <summary>
/// Consulta del cronograma (TAREA-20): valida el rango (D5, D6, D12), aplica la visibilidad R1 (D10: Admin todo; si no,
/// solo los proyectos propios) y arma la respuesta con <see cref="ArmadoCronograma"/>. Ver docs/fases/FASE_5_Cronograma.md.
/// </summary>
public sealed class CronogramaConsultaServicio(ICronogramaConsultas consultas, IUsuarioActual usuario, TimeProvider reloj)
{
    /// <summary>Valor de la semilla de CRONOGRAMA_MAX_DIAS si el parámetro faltara.</summary>
    public const int MaxDiasPorDefecto = 90;

    /// <summary>GET /api/cronograma.</summary>
    public async Task<ResultadoConsulta<CronogramaDto>> ConsultarAsync(CronogramaSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var errores = new Dictionary<string, string[]>();
        var desde = LectorParametros.LeerFecha(s.Desde, "desde", errores);
        var hasta = LectorParametros.LeerFecha(s.Hasta, "hasta", errores);
        var proyectoId = LectorParametros.LeerId(s.ProyectoId, "proyectoId", errores);
        var departamentoId = LectorParametros.LeerId(s.DepartamentoId, "departamentoId", errores);
        var maxDias = await MaxDiasAsync(ct);
        if (errores.Count > 0)
        {
            return ResultadoConsulta<CronogramaDto>.Invalido(errores);
        }

        // D6: sin fechas, el mes actual en Ecuador; con una sola, el mes de esa fecha.
        var referencia = desde ?? hasta ?? FechaNegocio.Hoy(reloj);
        var inicio = desde ?? new DateOnly(referencia.Year, referencia.Month, 1);
        var fin = hasta ?? new DateOnly(inicio.Year, inicio.Month, 1).AddMonths(1).AddDays(-1);
        if (ValidarRango(inicio, fin, maxDias) is { } invalido)
        {
            return invalido;
        }

        if (!TryObtenerVisibilidad(out var propietario))
        {
            return ResultadoConsulta<CronogramaDto>.Ok(new CronogramaDto(inicio, fin, maxDias, [], [], []));
        }

        var filtro = new FiltroCronograma(inicio, fin, propietario, ProyectoId: proyectoId, DepartamentoId: departamentoId,
            CodigoEkon: LectorParametros.Normalizar(s.CodigoEkon));
        // D2: para el Admin todo es visible; para los demás, se cuentan los días en proyectos de otros propietarios.
        return ResultadoConsulta<CronogramaDto>.Ok(await ArmarAsync(filtro, maxDias, propietario, salvoProyectoId: null, ct));
    }

    /// <summary>GET /api/proyectos/{id}/cronograma. Null si el proyecto no existe o no es visible (404, nunca 403).</summary>
    public async Task<ResultadoConsulta<CronogramaDto>?> ConsultarProyectoAsync(int proyectoId, ProyectoCronogramaSolicitud s,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!TryObtenerVisibilidad(out var propietario)
            || await consultas.ObtenerRangoProyectoAsync(proyectoId, propietario, ct) is not { } proyecto)
        {
            return null;
        }

        var errores = new Dictionary<string, string[]>();
        var desde = LectorParametros.LeerFecha(s.Desde, "desde", errores);
        var hasta = LectorParametros.LeerFecha(s.Hasta, "hasta", errores);
        var maxDias = await MaxDiasAsync(ct);
        if (errores.Count > 0)
        {
            return ResultadoConsulta<CronogramaDto>.Invalido(errores);
        }

        // D12: sin fechas, desde el inicio del proyecto hasta su fin, como máximo CRONOGRAMA_MAX_DIAS días.
        var inicio = desde ?? proyecto.FechaInicio;
        var finMaximo = inicio.AddDays(maxDias - 1);
        var fin = hasta ?? (proyecto.FechaFin >= inicio && proyecto.FechaFin < finMaximo ? proyecto.FechaFin : finMaximo);
        if (ValidarRango(inicio, fin, maxDias) is { } invalido)
        {
            return invalido;
        }

        var filtro = new FiltroCronograma(inicio, fin, propietario, SoloProyectoId: proyectoId);
        return ResultadoConsulta<CronogramaDto>.Ok(await ArmarAsync(filtro, maxDias, propietarioOtros: null, salvoProyectoId: proyectoId, ct));
    }

    private async Task<CronogramaDto> ArmarAsync(FiltroCronograma filtro, int maxDias, int? propietarioOtros, int? salvoProyectoId,
        CancellationToken ct)
    {
        var dias = await consultas.ObtenerDiasAsync(filtro, ct);
        if (dias.Count == 0)
        {
            return new CronogramaDto(filtro.Desde, filtro.Hasta, maxDias, [], [], []);
        }

        var empleados = dias.Select(d => d.EmpleadoId).Distinct().ToList();
        var proyectos = dias.Select(d => d.ProyectoId).Distinct().ToList();
        // Admin en el cronograma global: todo es visible, no hay "otros proyectos".
        var otros = propietarioOtros is null && salvoProyectoId is null
            ? []
            : await consultas.ObtenerOtrosProyectosAsync(
                new FiltroOtrosProyectos(empleados, filtro.Desde, filtro.Hasta, propietarioOtros, salvoProyectoId), ct);
        var novedades = await consultas.ObtenerNovedadesAsync(empleados, proyectos, filtro.Desde, filtro.Hasta, ct);
        var actividades = await consultas.ObtenerActividadesAsync(proyectos, filtro.Desde, filtro.Hasta, ct);
        var cabeceras = await consultas.ObtenerProyectosAsync(proyectos, ct);

        return ArmadoCronograma.Armar(filtro.Desde, filtro.Hasta, maxDias, dias, otros, novedades, actividades, cabeceras);
    }

    /// <summary>D5: hasta ≥ desde y como máximo CRONOGRAMA_MAX_DIAS días; si no, 400 en "hasta".</summary>
    private static ResultadoConsulta<CronogramaDto>? ValidarRango(DateOnly desde, DateOnly hasta, int maxDias)
    {
        if (hasta < desde)
        {
            return Error("La fecha 'hasta' no puede ser anterior a la fecha 'desde'.");
        }

        return hasta.DayNumber - desde.DayNumber + 1 > maxDias ? Error($"El rango no puede superar {maxDias} días.") : null;

        static ResultadoConsulta<CronogramaDto> Error(string mensaje) =>
            ResultadoConsulta<CronogramaDto>.Invalido(new Dictionary<string, string[]> { ["hasta"] = [mensaje] });
    }

    private async Task<int> MaxDiasAsync(CancellationToken ct) =>
        await consultas.ObtenerMaxDiasAsync(ct) is int n && n > 0 ? n : MaxDiasPorDefecto;

    /// <summary>R1 (D10): Admin ve todos (null); cualquier otro rol solo sus proyectos. False sin usuario resuelto.</summary>
    private bool TryObtenerVisibilidad(out int? propietarioUsuarioId)
    {
        if (usuario.TieneRol(RolesApp.Admin))
        {
            propietarioUsuarioId = null;
            return true;
        }

        propietarioUsuarioId = usuario.UsuarioId;
        return propietarioUsuarioId is not null;
    }
}
