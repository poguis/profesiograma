namespace App.Application.Diagnostics;

/// <summary>Resultado del diagnóstico de conexión y compatibilidad de SQL Server.</summary>
public sealed record DatabaseInfo(
    string Servidor,
    string BaseDatos,
    string UsuarioConectado,
    string VersionProducto,
    int VersionMayor,
    string NombreVersion,
    string NivelProducto,
    string Edicion,
    int NivelCompatibilidad,
    string Collation,
    bool PuedeCrearTablas,
    bool PuedeCrearEsquemas,
    bool EsDbOwner,
    bool CumpleRequisitos,
    IReadOnlyList<string> Observaciones);
