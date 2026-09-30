using System.Globalization;
using App.Domain.Proyectos.Cronograma;

namespace App.Domain.Tests.Proyectos.Cronograma;

/// <summary>Atajos para escribir las pruebas con fechas y valores esperados exactos.</summary>
internal static class Cron
{
    public const RolCronograma Pri = RolCronograma.Principal;
    public const RolCronograma Bck = RolCronograma.Back;
    public const RolCronograma Des = RolCronograma.Descanso;
    public const TipoAsignacionCronograma Auto = TipoAsignacionCronograma.Auto;
    public const TipoAsignacionCronograma Man = TipoAsignacionCronograma.Manual;

    public static readonly PersonaProyecto P1 = new(RolCronograma.Principal, 1);
    public static readonly PersonaProyecto P2 = new(RolCronograma.Principal, 2);
    public static readonly PersonaProyecto K1 = new(RolCronograma.Back, 1);

    /// <summary>"yyyy-MM-dd" → DateOnly.</summary>
    public static DateOnly F(string fecha) => DateOnly.ParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static PrincipalEntrada Principal(short numero, int empleadoId, string inicio, string fin, byte trabajo, byte descanso)
        => new(numero, empleadoId, F(inicio), F(fin), trabajo, descanso);

    public static BackEntrada Back(short numero, int empleadoId, string inicio, string fin, TipoRegistroBack tipo, byte descanso)
        => new(numero, empleadoId, F(inicio), F(fin), tipo, descanso);

    /// <summary>Proyecto 01/10/2026–31/10/2026 salvo que se indique otro rango.</summary>
    public static SolicitudCronograma Solicitud(
        PrincipalEntrada[]? principales = null,
        BackEntrada[]? backs = null,
        string inicio = "2026-10-01",
        string fin = "2026-10-31")
        => new(F(inicio), F(fin), principales ?? [], backs ?? []);

    public static Tramo T(RolCronograma rol, TipoAsignacionCronograma tipo, short bloque, PersonaProyecto persona,
        int empleadoId, string inicio, string fin)
        => new(rol, tipo, bloque, persona, empleadoId, F(inicio), F(fin));

    /// <summary>Días esperados de desde a hasta (inclusive), en orden, con los mismos datos.</summary>
    public static IEnumerable<DiaAsignado> D(int empleadoId, string desde, string hasta, RolCronograma rol,
        TipoAsignacionCronograma tipo, short bloque, PersonaProyecto persona)
    {
        for (var fecha = F(desde); fecha <= F(hasta); fecha = fecha.AddDays(1))
        {
            yield return new DiaAsignado(empleadoId, fecha, rol, tipo, bloque, persona);
        }
    }

    /// <summary>Días esperados de un tramo.</summary>
    public static IEnumerable<DiaAsignado> D(Tramo t) =>
        D(t.EmpleadoId, t.Inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            t.Fin.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), t.Rol, t.Tipo, t.Bloque, t.Persona);

    /// <summary>Concatena los días de varios tramos, en el orden dado.</summary>
    public static List<DiaAsignado> Dias(params Tramo[] tramos) => tramos.SelectMany(D).ToList();
}
