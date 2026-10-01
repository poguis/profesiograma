using App.Application.Proyectos.Crear;

namespace App.Application.Tests.Proyectos.Crear;

/// <summary>
/// Regresión TAREA-14: el snapshot de la etapa v1 (creación) conserva EXACTAMENTE el JSON anterior al refactor que
/// llevó su armado a SnapshotPersonal (compartido con suspensión y cierre). Sin cédula ni correo.
/// </summary>
public class SnapshotCreacionTests
{
    private const string Esperado =
        """[{"numero":1,"rol":"PRINCIPAL","ekon":"DEV006","nombre":"EMPLEADO PRUEBA 06","fechaInicio":"2026-12-01","fechaFin":"2026-12-31","jornada":"TIPO_2","diasTrabajo":11,"diasDescanso":4,"tipoRegistro":"JORNADA"},"""
        + """{"numero":2,"rol":"PRINCIPAL","ekon":"DEV007","nombre":"EMPLEADO PRUEBA 07","fechaInicio":"2026-12-01","fechaFin":"2026-12-31","jornada":"TIPO_3","diasTrabajo":5,"diasDescanso":2,"tipoRegistro":"JORNADA"},"""
        + """{"numero":1,"rol":"BACK","ekon":"DEV008","nombre":"EMPLEADO PRUEBA 08","fechaInicio":"2026-12-12","fechaFin":"2026-12-15","jornada":null,"diasTrabajo":null,"diasDescanso":2,"tipoRegistro":"JORNADA"}]""";

    [Fact]
    public async Task Creacion_SnapshotIdenticoAlAnterior()
    {
        var repo = new RepositorioFalso();
        var servicio = new CrearProyectoServicio(
            new CrearProyectoValidador(new DatosFalsos(), new ErpFalso(), new UsuarioFalso()),
            new CrucesExternosFalsos(), repo, new TransaccionFalsa(),
            new RelojFijo(new DateTimeOffset(2026, 12, 1, 15, 0, 0, TimeSpan.Zero)));

        await servicio.RegistrarAsync(Dobles.SolicitudCampo(), TestContext.Current.CancellationToken);

        Assert.Equal(Esperado, repo.Agregado!.Value.Proyecto.SnapshotPersonal);
    }
}
