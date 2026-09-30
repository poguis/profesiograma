namespace App.Application.Catalogos;

public sealed record ItemCatalogoDto(int Id, string Codigo, string Nombre, int Orden);

public sealed record EstadoProyectoDto(int Id, string Codigo, string Nombre, bool EsVigente, int Orden);

public sealed record GrupoProyectoDto(int Id, string Codigo, string Nombre, bool RequiereProyectoErp, bool RequiereDimension, int Orden);

public sealed record JornadaDto(int Id, string Codigo, string Nombre, int DiasTrabajo, int DiasDescanso, int Orden);

public sealed record RolAsignacionDto(int Id, string Codigo, string Nombre, bool EsDescanso, int Orden);

public sealed record OrigenNovedadDto(int Id, string Codigo, string Nombre, bool EsEditable, int Orden);

public sealed record TipoNovedadDto(
    int Id, string Codigo, string Nombre, string SiglaCronograma, string ColorHex,
    bool AplicaPersona, bool AplicaProyecto, bool AplicaGeneral, bool SeleccionableManual, int Orden);

public sealed record DepartamentoDto(int Id, string Nombre, string NombreCorto, string Tipo, int Orden);

public sealed record CatalogosDto(
    IReadOnlyList<EstadoProyectoDto> EstadosProyecto,
    IReadOnlyList<ItemCatalogoDto> TiposMovimiento,
    IReadOnlyList<GrupoProyectoDto> GruposProyecto,
    IReadOnlyList<JornadaDto> Jornadas,
    IReadOnlyList<RolAsignacionDto> RolesAsignacion,
    IReadOnlyList<ItemCatalogoDto> TiposAplicacionNovedad,
    IReadOnlyList<OrigenNovedadDto> OrigenesNovedad,
    IReadOnlyList<TipoNovedadDto> TiposNovedad,
    IReadOnlyList<DepartamentoDto> Departamentos);

public sealed record ParametroDto(string Clave, string Valor, string TipoDato, string? Descripcion);
