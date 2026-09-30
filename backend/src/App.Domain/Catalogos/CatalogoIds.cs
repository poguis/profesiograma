namespace App.Domain.Catalogos;

/// <summary>
/// Ids fijos de los catálogos (coinciden con las semillas de la migración).
/// Uso: CatalogoIds.EstadoProyecto.Activo
/// </summary>
public static class CatalogoIds
{
    public static class EstadoProyecto
    {
        public const byte Activo = 1, Suspendido = 2, Inactivo = 3, Terminado = 4;
    }

    public static class TipoMovimiento
    {
        public const byte Creacion = 1, ActualizacionPersonal = 2, Suspension = 3, Cierre = 4,
                          Reactivacion = 5, CambioActividad = 6, EdicionCabecera = 7;
    }

    public static class GrupoProyecto
    {
        public const byte Campo = 1, Planta = 2, OficinasAdministrativas = 3;
    }

    public static class Jornada
    {
        public const byte Tipo1 = 1, Tipo2 = 2, Tipo3 = 3, Especial = 4;
    }

    public static class RolAsignacion
    {
        public const byte Principal = 1, Back = 2, Descanso = 3;
    }

    public static class TipoAplicacionNovedad
    {
        public const byte Persona = 1, Proyecto = 2, General = 3;
    }

    public static class OrigenNovedad
    {
        public const byte Profesiograma = 1, PermisosMedicos = 2;
    }

    public static class TipoNovedad
    {
        public const byte CalamidadDomestica = 1, Descanso = 2, Feriado = 3, Paternidad = 4,
                          Permiso = 5, Vacaciones = 6, PermisoMedico = 7;
    }
}
