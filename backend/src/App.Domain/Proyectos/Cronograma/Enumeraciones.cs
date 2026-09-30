using App.Domain.Catalogos;

namespace App.Domain.Proyectos.Cronograma;

/// <summary>Rol de un día o tramo. Los valores son los Id de CatalogoIds.RolAsignacion (se guardan tal cual).</summary>
public enum RolCronograma : byte
{
    Principal = CatalogoIds.RolAsignacion.Principal,
    Back = CatalogoIds.RolAsignacion.Back,
    Descanso = CatalogoIds.RolAsignacion.Descanso,
}

/// <summary>AUTO = generado por jornada (principal); MANUAL = definido por el usuario (back). Ver ProyectoAsignacionDia.TipoAuto/TipoManual.</summary>
public enum TipoAsignacionCronograma
{
    Auto,
    Manual,
}

/// <summary>Tipo de registro de un back. Ver ProyectoPersonal.TipoRegistroJornada/TipoRegistroDescanso.</summary>
public enum TipoRegistroBack
{
    Jornada,
    Descanso,
}
