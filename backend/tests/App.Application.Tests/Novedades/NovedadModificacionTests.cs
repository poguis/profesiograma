using App.Application.Empleados;
using App.Application.Novedades;
using App.Application.Proyectos.Estados;
using App.Application.Seguridad;
using App.Application.Tests.Proyectos.Crear;
using App.Domain.Catalogos;

namespace App.Application.Tests.Novedades;

/// <summary>
/// TAREA-23-2: editar y anular. Orden de errores H5, N3, N8, N9, N10, N7 en la edición (R6) y conflictos. Dobles: la
/// fuente de empleados LANZA si se la llama (la edición no consulta la API).
/// </summary>
public sealed class NovedadModificacionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly byte[] V1 = [0, 0, 0, 0, 0, 0, 7, 209];
    private static readonly string Token = Convert.ToBase64String(V1); // "AAAAAAAAB9E="
    private static readonly DateOnly D1 = new(2032, 1, 1), D2 = new(2032, 1, 2), D3 = new(2032, 1, 3);

    // ------------------------------------------------------------------ dobles

    private sealed class EmpleadosQueNoSeUsan : IFuenteEmpleadosErp
    {
        public Task<ListaEmpleadosErp> ObtenerActivosAsync(CancellationToken ct) => throw new InvalidOperationException("La edición no llama a la API.");
        public Task<ListaEmpleadosErp> ObtenerActivosFrescosAsync(CancellationToken ct) => throw new InvalidOperationException("La edición no llama a la API.");
    }

    /// <summary>Lecturas: la 1.ª es fuera de la transacción; las siguientes, dentro (si hay otra en la cola, se usa).</summary>
    private sealed class ModificacionFalsa : INovedadModificacion
    {
        public Queue<NovedadParaModificar?> Lecturas { get; } = new();
        public NovedadParaModificar? Ultima { get; set; }
        public List<(int Id, int? Visible)> Pedidos { get; } = [];
        public EdicionNovedad? Editada { get; private set; }
        public AnulacionNovedad? Anulada { get; private set; }
        public bool Conflicto { get; set; }

        public Task<NovedadParaModificar?> ObtenerParaModificarAsync(int id, int? visible, CancellationToken ct)
        {
            Pedidos.Add((id, visible));
            if (Lecturas.Count > 0)
            {
                Ultima = Lecturas.Dequeue();
            }

            return Task.FromResult(Ultima);
        }

        public Task<string> EditarAsync(EdicionNovedad edicion, CancellationToken ct)
        {
            if (Conflicto) throw new ConflictoConcurrenciaException("otro proceso", new InvalidOperationException());
            Editada = edicion;
            return Task.FromResult("AAAAAAAAB9I=");
        }

        public Task<string> AnularAsync(AnulacionNovedad anulacion, CancellationToken ct)
        {
            if (Conflicto) throw new ConflictoConcurrenciaException("otro proceso", new InvalidOperationException());
            Anulada = anulacion;
            return Task.FromResult("AAAAAAAAB9M=");
        }
    }

    private sealed class Escenario
    {
        public Escenario(IUsuarioActual usuario, NovedadParaModificar? guardada)
        {
            Modificacion.Lecturas.Enqueue(guardada);
            var validador = new NovedadValidador(Consultas, new DatosFalsos(), new EmpleadosQueNoSeUsan(), usuario);
            Servicio = new NovedadModificacionServicio(validador, Modificacion, Repositorio, Transaccion, usuario, NovedadPrueba.Reloj);
        }

        public NovedadConsultasFalsas Consultas { get; } = new();
        public ModificacionFalsa Modificacion { get; } = new();
        public NovedadRepositorioFalso Repositorio { get; } = new();
        public TransaccionFalsa Transaccion { get; } = new();
        public NovedadModificacionServicio Servicio { get; }
    }

    private static NovedadParaModificar Guardada(
        byte aplicacion = CatalogoIds.TipoAplicacionNovedad.Persona, int creadoPor = 3, int? propietario = null,
        bool origenEditable = true, bool anulada = false, byte[]? rowVer = null, byte tipo = CatalogoIds.TipoNovedad.Vacaciones,
        string? observacion = "Prueba") =>
        new(5, aplicacion, tipo, aplicacion == CatalogoIds.TipoAplicacionNovedad.Persona ? 1 : null,
            aplicacion == CatalogoIds.TipoAplicacionNovedad.Proyecto ? 10 : null,
            origenEditable ? "PROFESIOGRAMA" : "PERMISOS_MEDICOS", origenEditable, propietario, anulada, creadoPor, observacion,
            [D1, D2, D3], rowVer ?? V1);

    private static EditarNovedadSolicitud Edicion(string? version = null, string tipo = "VACACIONES", DateOnly? inicio = null,
        DateOnly? fin = null, IReadOnlyList<DateOnly>? excluidos = null, string? observacion = "Prueba") =>
        new(version ?? Token, tipo, inicio ?? D1, fin ?? D3, excluidos ?? [D2], observacion);

    // ------------------------------------------------------------------ versión (N9) y orden H5

    [Theory]
    [InlineData(null, "Falta la versión de la novedad; vuelve a cargarla.")]
    [InlineData("  ", "Falta la versión de la novedad; vuelve a cargarla.")]
    [InlineData("no-es-base64!", "La versión de la novedad no es válida; vuelve a cargarla.")]
    [InlineData("AAAA", "La versión de la novedad no es válida; vuelve a cargarla.")]          // 3 bytes
    [InlineData("AAAAAAAAAAAAAA==", "La versión de la novedad no es válida; vuelve a cargarla.")] // 10 bytes
    public async Task Version_AusenteIlegibleOLargoIncorrecto_400_SinLeerLaNovedad(string? version, string mensaje)
    {
        var e = new Escenario(UsuarioNovedad.Admin(), Guardada());

        var editar = await e.Servicio.EditarAsync(5, Edicion() with { Version = version }, Ct);
        var anular = await e.Servicio.AnularAsync(5, new AnularNovedadSolicitud(version, "Motivo válido"), Ct);

        Assert.Equal(EstadoModificacionNovedad.Invalido, editar.Estado);
        Assert.Equal([mensaje], editar.Errores!["version"]);
        Assert.Equal([mensaje], anular.Errores!["version"]);
        Assert.Empty(e.Modificacion.Pedidos);
    }

    [Fact]
    public async Task NoExisteONoVisible_404_ConLaVisibilidadDelGestor()
    {
        var e = new Escenario(UsuarioNovedad.Gestor(), null);

        var r = await e.Servicio.EditarAsync(5, Edicion(), Ct);

        Assert.Equal(EstadoModificacionNovedad.NoEncontrada, r.Estado);
        Assert.Equal((5, (int?)3), e.Modificacion.Pedidos[0]);
    }

    [Fact]
    public async Task H5_OrigenAntesQuePermiso_PermisoAntesQueAnulada_AnuladaAntesQueVersion_VersionAntesQueCampos()
    {
        var ajena = new byte[] { 0, 0, 0, 0, 0, 0, 9, 9 };
        // PERMISOS_MEDICOS, ajena para el gestor, anulada y con otra versión: gana el origen (400).
        var origen = await new Escenario(UsuarioNovedad.Gestor(), Guardada(creadoPor: 9, origenEditable: false, anulada: true, rowVer: ajena))
            .Servicio.EditarAsync(5, Edicion(fin: D1.AddDays(-1)), Ct);
        // Editable, ajena, anulada, otra versión: 403.
        var permiso = await new Escenario(UsuarioNovedad.Gestor(), Guardada(creadoPor: 9, anulada: true, rowVer: ajena))
            .Servicio.EditarAsync(5, Edicion(fin: D1.AddDays(-1)), Ct);
        // Propia, anulada, otra versión: 409 anulada.
        var anulada = await new Escenario(UsuarioNovedad.Gestor(), Guardada(anulada: true, rowVer: ajena))
            .Servicio.EditarAsync(5, Edicion(fin: D1.AddDays(-1)), Ct);
        // Propia, vigente, otra versión, campos inválidos: 409 cambió (antes que los 400 de campos).
        var version = await new Escenario(UsuarioNovedad.Gestor(), Guardada(rowVer: ajena))
            .Servicio.EditarAsync(5, Edicion(fin: D1.AddDays(-1)), Ct);

        Assert.Equal(["Las novedades de PERMISOS_MEDICOS son de solo lectura."], origen.Errores!["origen"]);
        Assert.Equal(EstadoModificacionNovedad.Prohibido, permiso.Estado);
        Assert.Equal((EstadoModificacionNovedad.Conflicto, "La novedad está anulada."), (anulada.Estado, anulada.MensajeConflicto));
        Assert.Equal((EstadoModificacionNovedad.Conflicto, "La novedad cambió; vuelve a cargarla."), (version.Estado, version.MensajeConflicto));
    }

    [Fact]
    public async Task TipoConflicto_AnuladaYCambio_AlEditarYAlAnular()
    {
        var ajena = new byte[] { 0, 0, 0, 0, 0, 0, 9, 9 };
        var editarAnulada = await new Escenario(UsuarioNovedad.Gestor(), Guardada(anulada: true)).Servicio.EditarAsync(5, Edicion(), Ct);
        var editarCambio = await new Escenario(UsuarioNovedad.Gestor(), Guardada(rowVer: ajena)).Servicio.EditarAsync(5, Edicion(), Ct);
        var anularYa = await new Escenario(UsuarioNovedad.Gestor(), Guardada(anulada: true))
            .Servicio.AnularAsync(5, new AnularNovedadSolicitud(Token, "Motivo de prueba"), Ct);

        Assert.Equal((TipoConflictoNovedad.Anulada, PermisosNovedad.MensajeAnulada), (editarAnulada.TipoConflicto, editarAnulada.MensajeConflicto));
        Assert.Equal((TipoConflictoNovedad.Cambio, VersionNovedad.MensajeCambio), (editarCambio.TipoConflicto, editarCambio.MensajeConflicto));
        Assert.Equal((TipoConflictoNovedad.Anulada, ResultadoModificacionNovedad.MensajeYaAnulada), (anularYa.TipoConflicto, anularYa.MensajeConflicto));
    }

    [Theory]
    [InlineData(CatalogoIds.TipoAplicacionNovedad.Persona, 3, null, true)]   // creador
    [InlineData(CatalogoIds.TipoAplicacionNovedad.Persona, 9, null, false)]
    [InlineData(CatalogoIds.TipoAplicacionNovedad.Proyecto, 9, 3, true)]     // propietario del proyecto
    [InlineData(CatalogoIds.TipoAplicacionNovedad.Proyecto, 3, 9, false)]    // creó la novedad, pero el proyecto es de otro
    [InlineData(CatalogoIds.TipoAplicacionNovedad.General, 3, null, false)]  // solo Admin
    public async Task N3_MatrizDePermisos_Gestor(byte aplicacion, int creadoPor, int? propietario, bool puede)
    {
        var tipo = aplicacion == CatalogoIds.TipoAplicacionNovedad.Persona ? CatalogoIds.TipoNovedad.Vacaciones : CatalogoIds.TipoNovedad.Feriado;
        var e = new Escenario(UsuarioNovedad.Gestor(), Guardada(aplicacion, creadoPor, propietario, tipo: tipo));

        var r = await e.Servicio.EditarAsync(5, Edicion(tipo: tipo == CatalogoIds.TipoNovedad.Feriado ? "FERIADO" : "VACACIONES"), Ct);

        Assert.Equal(puede, r.Estado == EstadoModificacionNovedad.Hecho);
        Assert.Equal(!puede, r.Estado == EstadoModificacionNovedad.Prohibido);
    }

    [Fact]
    public async Task Admin_EditaGeneral()
    {
        var e = new Escenario(UsuarioNovedad.Admin(), Guardada(CatalogoIds.TipoAplicacionNovedad.General, tipo: CatalogoIds.TipoNovedad.Feriado));

        Assert.Equal(EstadoModificacionNovedad.Hecho, (await e.Servicio.EditarAsync(5, Edicion(tipo: "FERIADO"), Ct)).Estado);
        Assert.Null(e.Modificacion.Pedidos[0].Visible);
    }

    // ------------------------------------------------------------------ campos (N8)

    [Theory]
    [InlineData("DESCANSO", "tipoNovedad", "El tipo de novedad no aplica a PERSONA.")] // RN16 con la aplicación GUARDADA
    [InlineData("FERIADO", "tipoNovedad", "El tipo de novedad no aplica a PERSONA.")]
    [InlineData("NO_EXISTE", "tipoNovedad", "El tipo de novedad no es válido.")]
    public async Task Tipo_Rn16(string tipo, string campo, string mensaje)
    {
        var r = await new Escenario(UsuarioNovedad.Gestor(), Guardada()).Servicio.EditarAsync(5, Edicion(tipo: tipo), Ct);

        Assert.Equal([mensaje], r.Errores![campo]);
    }

    [Fact]
    public async Task Fechas_YExcluidos_400()
    {
        async Task<IReadOnlyDictionary<string, string[]>> Errores(EditarNovedadSolicitud s) =>
            (await new Escenario(UsuarioNovedad.Gestor(), Guardada()).Servicio.EditarAsync(5, s, Ct)).Errores!;

        Assert.Equal(["La fecha de inicio es obligatoria."], (await Errores(Edicion() with { FechaInicio = null }))["fechaInicio"]);
        Assert.Equal(["La fecha fin debe ser mayor o igual a la fecha inicio."], (await Errores(Edicion(inicio: D3, fin: D1)))["fechaFin"]);
        Assert.Equal(["La novedad no puede superar 366 días."], (await Errores(Edicion(fin: D1.AddDays(366))))["fechaFin"]);
        Assert.Equal(["Los días excluidos deben estar dentro del rango."], (await Errores(Edicion(fin: D2, excluidos: [D3])))["diasExcluidos"]);
        Assert.Equal(["La novedad debe tener al menos 1 día."], (await Errores(Edicion(fin: D2, excluidos: [D1, D2, D1])))["diasExcluidos"]);
        Assert.Equal(["La observación no puede superar 500 caracteres."], (await Errores(Edicion(observacion: new string('x', 501))))["observacion"]);
    }

    [Fact]
    public async Task SinCambios_400General_YObservacionVaciaLaBorra()
    {
        // Guardada: VACACIONES, días D1 D2 D3, observación "Prueba".
        var sinCambios = await new Escenario(UsuarioNovedad.Gestor(), Guardada()).Servicio.EditarAsync(5, Edicion(excluidos: [], observacion: " Prueba "), Ct);
        var borrar = new Escenario(UsuarioNovedad.Gestor(), Guardada());
        var r = await borrar.Servicio.EditarAsync(5, Edicion(excluidos: [], observacion: "   "), Ct);

        Assert.Equal(["No hay cambios para registrar."], sinCambios.Errores!["general"]);
        Assert.Equal(EstadoModificacionNovedad.Hecho, r.Estado);
        Assert.Null(borrar.Modificacion.Editada!.Observacion);
    }

    [Fact]
    public async Task Editar_ExcluirElDiaDelMedio_DiasYVersionNueva_DentroDeLaTransaccion()
    {
        var e = new Escenario(UsuarioNovedad.Gestor(), Guardada());

        var r = await e.Servicio.EditarAsync(5, Edicion(tipo: "permiso", excluidos: [D2, D2]), Ct);

        Assert.Equal(new NovedadEditadaDto(5, "AAAAAAAAB9I="), r.Hecho);
        var ed = e.Modificacion.Editada!;
        Assert.Equal(CatalogoIds.TipoNovedad.Permiso, ed.TipoNovedadId);
        Assert.Equal([D1, D3], ed.Dias);
        Assert.Equal(V1, ed.Token); // R1: el token del cliente
        Assert.Equal((1, 1), (e.Transaccion.Iniciadas, e.Transaccion.Confirmadas));
        Assert.Equal(2, e.Modificacion.Pedidos.Count); // fuera y dentro de la transacción
    }

    // ------------------------------------------------------------------ dentro de la transacción y duplicado (N7, R6)

    [Fact]
    public async Task DentroDeLaTransaccion_VersionCambio_409_SinEscribir()
    {
        var e = new Escenario(UsuarioNovedad.Gestor(), Guardada());
        e.Modificacion.Lecturas.Enqueue(Guardada(rowVer: [0, 0, 0, 0, 0, 0, 7, 210]));

        var r = await e.Servicio.EditarAsync(5, Edicion(), Ct);

        Assert.Equal((EstadoModificacionNovedad.Conflicto, VersionNovedad.MensajeCambio), (r.Estado, r.MensajeConflicto));
        Assert.Null(e.Modificacion.Editada);
        Assert.Equal(1, e.Transaccion.Revertidas);
    }

    [Fact]
    public async Task ConflictoAlGuardar_409()
    {
        var e = new Escenario(UsuarioNovedad.Gestor(), Guardada());
        e.Modificacion.Conflicto = true;

        var editar = await e.Servicio.EditarAsync(5, Edicion(), Ct);
        var anular = await e.Servicio.AnularAsync(5, new AnularNovedadSolicitud(Token, "Motivo válido"), Ct);

        Assert.Equal(VersionNovedad.MensajeCambio, editar.MensajeConflicto);
        Assert.Equal(VersionNovedad.MensajeCambio, anular.MensajeConflicto);
    }

    [Theory]
    [InlineData(true, "NOV-20320101-aaaaaa")]
    [InlineData(false, null)]
    public async Task Duplicado_ConDiasResultantesYExcluyendoLaPropia_R6(bool visible, string? codigo)
    {
        var e = new Escenario(UsuarioNovedad.Gestor(), Guardada());
        e.Repositorio.Duplicado = new DuplicadoNovedad("NOV-20320101-aaaaaa", visible);

        var r = await e.Servicio.EditarAsync(5, Edicion(tipo: "PERMISO"), Ct);

        Assert.Equal((EstadoModificacionNovedad.Duplicado, codigo), (r.Estado, r.CodigoDuplicado));
        var (criterio, vis) = e.Repositorio.DuplicadoPedido!.Value;
        Assert.Equal((CatalogoIds.TipoNovedad.Permiso, (int?)1, D1, D3, (int?)5, (int?)3),
            (criterio.TipoNovedadId, criterio.EmpleadoId, criterio.Desde, criterio.Hasta, criterio.ExcluirNovedadId, vis));
        Assert.Equal([D1, D3], criterio.Dias!);
        Assert.Null(e.Modificacion.Editada);
    }

    // ------------------------------------------------------------------ anular (N10)

    [Theory]
    [InlineData(null)]
    [InlineData("  abc  ")]
    public async Task Anular_MotivoFueraDe5a500_400(string? motivo)
    {
        var r = await new Escenario(UsuarioNovedad.Gestor(), Guardada()).Servicio.AnularAsync(5, new AnularNovedadSolicitud(Token, motivo), Ct);

        Assert.Equal(["El motivo debe tener entre 5 y 500 caracteres."], r.Errores!["motivo"]);
    }

    [Fact]
    public async Task Anular_Exito_ConFechaUtcTruncadaYUsuario()
    {
        var e = new Escenario(UsuarioNovedad.Gestor(), Guardada());

        var r = await e.Servicio.AnularAsync(5, new AnularNovedadSolicitud(Token, "  Registrada por error  "), Ct);

        Assert.Equal(new NovedadEditadaDto(5, "AAAAAAAAB9M="), r.Hecho);
        var a = e.Modificacion.Anulada!;
        Assert.Equal(("Registrada por error", 3, new DateTime(2026, 10, 22, 3, 0, 0, DateTimeKind.Utc)), (a.Motivo, a.AnuladaPorId, a.FechaAnulacionUtc));
        Assert.Equal(V1, a.Token);
    }

    [Fact]
    public async Task Anular_YaAnulada_409_YOrigenNoEditable_400()
    {
        var ya = await new Escenario(UsuarioNovedad.Admin(), Guardada(anulada: true)).Servicio.AnularAsync(5, new AnularNovedadSolicitud(Token, "Motivo válido"), Ct);
        var pm = await new Escenario(UsuarioNovedad.Admin(), Guardada(origenEditable: false)).Servicio.AnularAsync(5, new AnularNovedadSolicitud(Token, "Motivo válido"), Ct);

        Assert.Equal((EstadoModificacionNovedad.Conflicto, "La novedad ya está anulada."), (ya.Estado, ya.MensajeConflicto));
        Assert.Equal(["Las novedades de PERMISOS_MEDICOS son de solo lectura."], pm.Errores!["origen"]);
    }

    [Fact]
    public async Task Anular_SeAnuloEntreLaLecturaYLaTransaccion_409()
    {
        var e = new Escenario(UsuarioNovedad.Gestor(), Guardada());
        e.Modificacion.Lecturas.Enqueue(Guardada(anulada: true));

        var r = await e.Servicio.AnularAsync(5, new AnularNovedadSolicitud(Token, "Motivo válido"), Ct);

        Assert.Equal("La novedad ya está anulada.", r.MensajeConflicto);
        Assert.Null(e.Modificacion.Anulada);
    }
}
