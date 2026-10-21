using App.Application.Erp;
using App.Application.Novedades;
using App.Application.Tests.Proyectos.Crear;
using App.Domain.Catalogos;
using App.Domain.Novedades;
using App.Domain.Proyectos;

namespace App.Application.Tests.Novedades;

/// <summary>TAREA-23-1: validación (RN16, sujeto, fechas), permisos (J1, J2, N3), duplicado (N7, R6) y creación (R7, D15).</summary>
public sealed class NovedadTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ------------------------------------------------------------------ RN16 y aplicación

    [Theory]
    [InlineData("PERSONA", "VACACIONES", null)]
    [InlineData("PERSONA", "CALAMIDAD_DOMESTICA", null)]
    [InlineData("persona", "vacaciones", null)] // sin distinguir mayúsculas
    [InlineData("PERSONA", "DESCANSO", "El tipo de novedad no aplica a PERSONA.")]       // SeleccionableManual = 0
    [InlineData("PERSONA", "FERIADO", "El tipo de novedad no aplica a PERSONA.")]
    [InlineData("PERSONA", "PERMISO_MEDICO", "El tipo de novedad no aplica a PERSONA.")] // solo por integración
    [InlineData("PROYECTO", "FERIADO", null)]
    [InlineData("PROYECTO", "VACACIONES", "El tipo de novedad no aplica a PROYECTO.")]
    [InlineData("GENERAL", "FERIADO", null)]
    [InlineData("GENERAL", "PERMISO", "El tipo de novedad no aplica a GENERAL.")]
    [InlineData("PERSONA", "INEXISTENTE", "El tipo de novedad no es válido.")]
    public async Task Rn16_TipoSegunAplicacion(string aplicacion, string tipo, string? error)
    {
        var e = new EscenarioNovedad(UsuarioNovedad.Admin());
        var s = aplicacion.ToUpperInvariant() switch
        {
            "PERSONA" => NovedadPrueba.Persona(tipo: tipo) with { TipoAplicacion = aplicacion },
            "PROYECTO" => NovedadPrueba.Proyecto(tipo: tipo),
            _ => NovedadPrueba.General(tipo),
        };

        var v = await e.Validador.ValidarAsync(s, Ct);

        if (error is null)
        {
            Assert.NotNull(v.Valida);
        }
        else
        {
            Assert.Equal([error], v.Errores["tipoNovedad"]);
        }
    }

    [Theory]
    [InlineData(null, "El tipo de aplicación es obligatorio.")]
    [InlineData("TODOS", "El tipo de aplicación no es válido.")]
    public async Task Aplicacion_FaltaOInvalida_400(string? aplicacion, string mensaje)
    {
        var v = await new EscenarioNovedad(UsuarioNovedad.Gestor()).Validador.ValidarAsync(NovedadPrueba.General() with { TipoAplicacion = aplicacion }, Ct);

        Assert.Equal([mensaje], v.Errores["tipoAplicacion"]);
    }

    [Fact]
    public async Task General_Gestor_Prohibido_SinLeerNadaNiAbrirLaTransaccion()
    {
        var e = new EscenarioNovedad(UsuarioNovedad.Gestor());

        var r = await e.Servicio.CrearAsync(NovedadPrueba.General(), Ct);

        Assert.Equal(EstadoCrearNovedad.Prohibido, r.Estado);
        Assert.Equal(0, e.Transaccion.Iniciadas);
        Assert.Null(e.Repositorio.Agregada);
    }

    [Fact]
    public async Task General_Admin_SeCrea_SinSujeto()
    {
        var e = new EscenarioNovedad(UsuarioNovedad.Admin());

        var r = await e.Servicio.CrearAsync(NovedadPrueba.General(), Ct);

        Assert.Equal(EstadoCrearNovedad.Creado, r.Estado);
        var d = e.Repositorio.Agregada!.Datos;
        Assert.Equal((CatalogoIds.TipoAplicacionNovedad.General, (int?)null, (int?)null), (d.AplicacionId, d.Empleado?.Id, d.ProyectoId));
    }

    // ------------------------------------------------------------------ sujeto

    [Fact]
    public async Task Sujeto_QueNoCorresponde_YFaltante_400EnSuCampo()
    {
        var v = await new EscenarioNovedad(UsuarioNovedad.Admin()).Validador.ValidarAsync(
            NovedadPrueba.General() with { CodigoEkon = "DEV001", ProyectoId = 10 }, Ct);
        var sinProyecto = await new EscenarioNovedad(UsuarioNovedad.Admin()).Validador.ValidarAsync(
            NovedadPrueba.Proyecto() with { ProyectoId = null }, Ct);
        var sinEmpleado = await new EscenarioNovedad(UsuarioNovedad.Admin()).Validador.ValidarAsync(
            NovedadPrueba.Persona() with { CodigoEkon = " " }, Ct);

        Assert.Equal(["No corresponde en una novedad GENERAL."], v.Errores["codigoEkon"]);
        Assert.Equal(["No corresponde en una novedad GENERAL."], v.Errores["proyectoId"]);
        Assert.Equal([NovedadValidador.MensajeProyectoObligatorio], sinProyecto.Errores["proyectoId"]);
        Assert.Equal(["El empleado es obligatorio."], sinEmpleado.Errores["codigoEkon"]);
    }

    [Theory]
    [InlineData(10, 3, true)]   // propio, vigente
    [InlineData(20, 3, false)]  // de otro usuario (R1)
    [InlineData(30, 3, false)]  // no existe, eliminado o no vigente
    [InlineData(20, null, true)] // Admin ve todos
    public async Task Proyecto_Elegible(int proyectoId, int? gestorId, bool valido)
    {
        var usuario = gestorId is int g ? UsuarioNovedad.Gestor(g) : UsuarioNovedad.Admin();

        var v = await new EscenarioNovedad(usuario).Validador.ValidarAsync(NovedadPrueba.Proyecto(proyectoId), Ct);

        Assert.Equal(valido, v.Valida is not null);
        if (!valido)
        {
            Assert.Equal(["El proyecto no existe, no está vigente o no tienes acceso."], v.Errores["proyectoId"]);
        }
    }

    [Theory]
    [InlineData("DEV999")] // no existe en la API
    [InlineData("DEV009")] // tiene fila pero ya no está activo (no está en la API)
    public async Task Persona_EmpleadoInexistenteOInactivo_400(string codigo)
    {
        var e = new EscenarioNovedad(UsuarioNovedad.Gestor());

        var r = await e.Servicio.CrearAsync(NovedadPrueba.Persona(codigo), Ct);

        Assert.Equal(EstadoCrearNovedad.Invalido, r.Estado);
        Assert.Equal(["El empleado no existe o no está activo."], r.Errores!["codigoEkon"]);
        Assert.Equal(0, e.Transaccion.Iniciadas);
    }

    [Fact]
    public async Task Persona_ApiCaida_503SinAbrirLaTransaccionNiEscribir()
    {
        var api = new EmpleadosErpFalsos { Error = new ErpNoDisponibleException("empleados", "sin respuesta") };
        var e = new EscenarioNovedad(UsuarioNovedad.Gestor(), api);

        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => e.Servicio.CrearAsync(NovedadPrueba.Persona(), Ct));

        Assert.Equal(0, e.Transaccion.Iniciadas);
        Assert.Null(e.Repositorio.Agregada);
    }

    [Fact]
    public async Task Persona_LecturaFresca_YConErroresDeFormaNoLlamaALaApi()
    {
        var api = new EmpleadosErpFalsos();
        var e = new EscenarioNovedad(UsuarioNovedad.Gestor(), api);

        await e.Servicio.CrearAsync(NovedadPrueba.Persona() with { FechaFin = new DateOnly(2026, 1, 1) }, Ct);
        Assert.Equal((0, 0), (api.Lecturas, api.LecturasFrescas));

        await e.Servicio.CrearAsync(NovedadPrueba.Persona(), Ct);
        Assert.Equal((0, 1), (api.Lecturas, api.LecturasFrescas));
    }

    [Fact]
    public async Task Persona_SinFila_AltaPuntualConCedulaSoloDeLaLecturaFresca()
    {
        var api = new FuenteEmpleadosConCedulas();
        var e = new EscenarioNovedad(UsuarioNovedad.Gestor(), api);

        var r = await e.Servicio.CrearAsync(NovedadPrueba.Persona("SIM001"), Ct);

        Assert.Equal(EstadoCrearNovedad.Creado, r.Estado);
        var d = e.Repositorio.Agregada!.Datos;
        Assert.True(d.Empleado!.Id < 0); // temporal: la alta puntual lo convierte en real
        Assert.NotNull(d.Empleado.Erp);
        Assert.Equal(FuenteEmpleadosConCedulas.CedulaFicticia, d.Cedulas.De("SIM001"));
        Assert.Equal(1, api.LecturasFrescas);
        Assert.Null(e.Repositorio.DuplicadoPedido); // sin fila: no puede tener novedades guardadas
        Assert.DoesNotContain(FuenteEmpleadosConCedulas.CedulaFicticia, d.ToString()); // D15: el ToString no la muestra
    }

    // ------------------------------------------------------------------ fechas y observación

    [Theory]
    [InlineData("2026-11-05", "2026-11-04", "fechaFin", "La fecha fin debe ser mayor o igual a la fecha inicio.")]
    [InlineData("2026-01-01", "2027-01-02", "fechaFin", "La novedad no puede superar 366 días.")] // 367 días
    [InlineData(null, "2026-11-04", "fechaInicio", "La fecha de inicio es obligatoria.")]
    [InlineData("2026-11-04", null, "fechaFin", "La fecha fin es obligatoria.")]
    public async Task Fechas_400(string? inicio, string? fin, string campo, string mensaje)
    {
        var s = NovedadPrueba.Persona() with
        {
            FechaInicio = inicio is null ? null : DateOnly.Parse(inicio, System.Globalization.CultureInfo.InvariantCulture),
            FechaFin = fin is null ? null : DateOnly.Parse(fin, System.Globalization.CultureInfo.InvariantCulture),
        };

        var v = await new EscenarioNovedad(UsuarioNovedad.Gestor()).Validador.ValidarAsync(s, Ct);

        Assert.Equal([mensaje], v.Errores[campo]);
    }

    [Fact]
    public async Task Fechas_366Dias_YPasadas_SePermiten()
    {
        var e = new EscenarioNovedad(UsuarioNovedad.Gestor());

        var v = await e.Validador.ValidarAsync(NovedadPrueba.Persona() with
        {
            FechaInicio = new DateOnly(2020, 1, 1), FechaFin = new DateOnly(2020, 12, 31), // 2020 es bisiesto: 366 días
        }, Ct);

        Assert.NotNull(v.Valida);
    }

    [Fact]
    public async Task Observacion_Recortada_YMasDe500_400()
    {
        var e = new EscenarioNovedad(UsuarioNovedad.Gestor());

        var ok = await e.Validador.ValidarAsync(NovedadPrueba.Persona() with { Observacion = "  texto  " }, Ct);
        var larga = await e.Validador.ValidarAsync(NovedadPrueba.Persona() with { Observacion = new string('x', 501) }, Ct);

        Assert.Equal("texto", ok.Valida!.Observacion);
        Assert.Equal(["La observación no puede superar 500 caracteres."], larga.Errores["observacion"]);
    }

    // ------------------------------------------------------------------ creación: días, código (R7) y duplicado (N7, R6)

    [Fact]
    public async Task Crear_DiasDelRango_CodigoConFechaDeEcuador_YRegistrante()
    {
        var e = new EscenarioNovedad(UsuarioNovedad.Gestor());

        var r = await e.Servicio.CrearAsync(NovedadPrueba.Persona(), Ct);

        Assert.Equal(EstadoCrearNovedad.Creado, r.Estado);
        var n = e.Repositorio.Agregada!;
        Assert.Equal([new(2026, 11, 2), new(2026, 11, 3), new(2026, 11, 4), new(2026, 11, 5)], n.Dias);
        Assert.Equal(GeneradorCodigoNovedad.Generar(new DateOnly(2026, 10, 21), n.Uid), n.Codigo);
        Assert.Equal(("GESTOR DESARROLLO", "gestor.dev@profesiograma.local"), (n.RegistradoPorNombre, n.RegistradoPorCorreo));
        Assert.Equal((1, 1, 0), (e.Transaccion.Iniciadas, e.Transaccion.Confirmadas, e.Transaccion.Revertidas));
        Assert.Equal(1, e.Repositorio.DuplicadoPedido!.Value.Criterio.EmpleadoId); // DEV001 tiene fila (Id 1)
        Assert.Equal(3, e.Repositorio.DuplicadoPedido.Value.Visible);
    }

    [Fact]
    public async Task Crear_CodigoOUidRepetido_ReintentaConOtroUid_YTrasTresLanza()
    {
        var reintento = new EscenarioNovedad(UsuarioNovedad.Admin(), null, true, false);
        Assert.Equal(EstadoCrearNovedad.Creado, (await reintento.Servicio.CrearAsync(NovedadPrueba.General(), Ct)).Estado);
        Assert.Equal(2, reintento.Repositorio.CodigosConsultados.Count);

        var agotado = new EscenarioNovedad(UsuarioNovedad.Admin(), null, true, true, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => agotado.Servicio.CrearAsync(NovedadPrueba.General(), Ct));
        Assert.Equal(1, agotado.Transaccion.Revertidas);
    }

    [Theory]
    [InlineData(true, "NOV-20261001-aaaaaa")]
    [InlineData(false, null)] // R6: si quien registra no la ve, no se informa el código
    public async Task Crear_Duplicado_409_CodigoSoloSiEsVisible(bool visible, string? codigo)
    {
        var e = new EscenarioNovedad(UsuarioNovedad.Gestor());
        e.Repositorio.Duplicado = new DuplicadoNovedad("NOV-20261001-aaaaaa", visible);

        var r = await e.Servicio.CrearAsync(NovedadPrueba.Persona(), Ct);

        Assert.Equal(EstadoCrearNovedad.Duplicado, r.Estado);
        Assert.Equal(codigo, r.CodigoDuplicado);
        Assert.Null(e.Repositorio.Agregada);
        Assert.Equal(1, e.Transaccion.Revertidas);
    }

    [Fact]
    public async Task Crear_Duplicado_CriterioConSujetoTipoYRango()
    {
        var e = new EscenarioNovedad(UsuarioNovedad.Admin());

        await e.Servicio.CrearAsync(NovedadPrueba.Proyecto(20), Ct);

        Assert.Equal(new CriterioDuplicado(CatalogoIds.TipoAplicacionNovedad.Proyecto, CatalogoIds.TipoNovedad.Feriado, null, 20,
            new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 2)), e.Repositorio.DuplicadoPedido!.Value.Criterio);
        Assert.Null(e.Repositorio.DuplicadoPedido.Value.Visible); // Admin ve todas
    }

    // ------------------------------------------------------------------ permisos (J1, J2, N3)

    [Fact]
    public void J1_SoloAdminRegistraGeneral()
    {
        Assert.True(PermisosNovedad.PuedeRegistrarGeneral(UsuarioNovedad.Admin()));
        Assert.False(PermisosNovedad.PuedeRegistrarGeneral(UsuarioNovedad.Gestor()));
    }

    [Theory]
    [InlineData(CatalogoIds.TipoAplicacionNovedad.Persona, CatalogoIds.OrigenNovedad.Profesiograma, 3, null, true)]  // creada por él
    [InlineData(CatalogoIds.TipoAplicacionNovedad.Persona, CatalogoIds.OrigenNovedad.Profesiograma, 9, null, false)] // PERSONA de otro
    [InlineData(CatalogoIds.TipoAplicacionNovedad.General, CatalogoIds.OrigenNovedad.Profesiograma, 1, null, true)]  // GENERAL
    [InlineData(CatalogoIds.TipoAplicacionNovedad.Proyecto, CatalogoIds.OrigenNovedad.Profesiograma, 9, 3, true)]    // su proyecto
    [InlineData(CatalogoIds.TipoAplicacionNovedad.Proyecto, CatalogoIds.OrigenNovedad.Profesiograma, 9, 9, false)]   // proyecto ajeno
    [InlineData(CatalogoIds.TipoAplicacionNovedad.Persona, CatalogoIds.OrigenNovedad.PermisosMedicos, 3, null, false)] // PERMISOS_MEDICOS
    public void J2_VisiblePara_Gestor3(byte aplicacion, byte origen, int creadoPor, int? propietario, bool visible)
    {
        var novedad = new Novedad
        {
            TipoAplicacionNovedadId = aplicacion,
            OrigenNovedadId = origen,
            CreadoPorId = creadoPor,
            Proyecto = propietario is int p ? new Proyecto { PropietarioUsuarioId = p } : null,
        };

        Assert.Equal(visible, PermisosNovedad.VisiblePara(3).Compile()(novedad));
    }

    [Fact]
    public void J2_ProyectoEliminado_NoVisible()
    {
        var novedad = new Novedad
        {
            TipoAplicacionNovedadId = CatalogoIds.TipoAplicacionNovedad.Proyecto,
            OrigenNovedadId = CatalogoIds.OrigenNovedad.Profesiograma,
            CreadoPorId = 9,
            Proyecto = new Proyecto { PropietarioUsuarioId = 3, Eliminado = true },
        };

        Assert.False(PermisosNovedad.VisiblePara(3).Compile()(novedad));
    }

    [Fact]
    public void N3_Modificacion()
    {
        var gestor = UsuarioNovedad.Gestor();
        var admin = UsuarioNovedad.Admin();

        Assert.Equal((true, (string?)null), PermisosNovedad.Modificacion(gestor, NovedadPrueba.Detalle(creadoPor: 3)));
        Assert.Equal((false, PermisosNovedad.MensajeSinPermiso), PermisosNovedad.Modificacion(gestor, NovedadPrueba.Detalle(creadoPor: 9)));
        Assert.True(PermisosNovedad.Modificacion(admin, NovedadPrueba.Detalle(creadoPor: 9)).Puede);
        Assert.True(PermisosNovedad.Modificacion(gestor, NovedadPrueba.Detalle(CatalogoIds.TipoAplicacionNovedad.Proyecto, 9, propietario: 3)).Puede);
        Assert.False(PermisosNovedad.Modificacion(gestor, NovedadPrueba.Detalle(CatalogoIds.TipoAplicacionNovedad.Proyecto, 3, propietario: 9)).Puede);
        Assert.False(PermisosNovedad.Modificacion(gestor, NovedadPrueba.Detalle(CatalogoIds.TipoAplicacionNovedad.General, 3)).Puede);
        Assert.True(PermisosNovedad.Modificacion(admin, NovedadPrueba.Detalle(CatalogoIds.TipoAplicacionNovedad.General, 3)).Puede);
        Assert.Equal((false, "Las novedades de PERMISOS_MEDICOS son de solo lectura."),
            PermisosNovedad.Modificacion(admin, NovedadPrueba.Detalle(origenEditable: false)));
        Assert.Equal((false, PermisosNovedad.MensajeAnulada), PermisosNovedad.Modificacion(admin, NovedadPrueba.Detalle(anulada: true)));
    }

    // ------------------------------------------------------------------ consulta y opciones

    [Fact]
    public async Task Detalle_RegistranteSoloAdmin_VersionBase64_YPermisos()
    {
        var consultas = new NovedadConsultasFalsas { Detalle = NovedadPrueba.Detalle(creadoPor: 9) };

        var gestor = await new NovedadConsultaServicio(consultas, UsuarioNovedad.Gestor()).ObtenerDetalleAsync(5, Ct);
        Assert.Equal((5, (int?)3), consultas.DetallePedido);
        var admin = await new NovedadConsultaServicio(consultas, UsuarioNovedad.Admin()).ObtenerDetalleAsync(5, Ct);
        Assert.Equal((5, (int?)null), consultas.DetallePedido);

        Assert.Null(gestor!.Registro);
        Assert.Equal(("PERSONA", "AAAAAAAAB9E=", false, false, PermisosNovedad.MensajeSinPermiso),
            (gestor.Aplicacion, gestor.Version, gestor.PuedeEditar, gestor.PuedeAnular, gestor.MotivoNoEditable));
        Assert.Equal(new RegistroNovedadDto("REGISTRANTE FICTICIO", "registrante@profesiograma.local"), admin!.Registro);
        Assert.True(admin.PuedeEditar);
        Assert.Null(await new NovedadConsultaServicio(consultas, UsuarioNovedad.Gestor()).ObtenerDetalleAsync(6, Ct));
    }

    [Theory]
    [InlineData("aplicacion", "TODAS")]
    [InlineData("desde", "21-10-2026")]
    [InlineData("incluirAnuladas", "si")]
    [InlineData("tamano", "101")]
    public async Task Listado_ValoresIlegibles_400EnSuCampo(string campo, string valor)
    {
        var s = new NovedadListadoSolicitud(
            campo == "aplicacion" ? valor : null, null, null, null,
            campo == "desde" ? valor : null, null, campo == "incluirAnuladas" ? valor : null, null, campo == "tamano" ? valor : null);

        var r = await new NovedadConsultaServicio(new NovedadConsultasFalsas(), UsuarioNovedad.Gestor()).ListarAsync(s, Ct);

        Assert.False(r.EsValido);
        Assert.True(r.Errores!.ContainsKey(campo));
    }

    [Fact]
    public async Task Listado_FiltroConVisibilidad_YSinAnuladasPorDefecto()
    {
        var consultas = new NovedadConsultasFalsas();

        await new NovedadConsultaServicio(consultas, UsuarioNovedad.Gestor()).ListarAsync(
            new NovedadListadoSolicitud("persona", "vacaciones", null, " NOV ", "2026-10-01", "2026-10-31", null, null, null), Ct);

        Assert.Equal(new NovedadFiltro(CatalogoIds.TipoAplicacionNovedad.Persona, "VACACIONES", null, "NOV",
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), false, 1, 20, 3), consultas.FiltroRecibido);
    }

    [Fact]
    public async Task Opciones_GestorSinGeneral_AdminConGeneral_TiposSegunRn16()
    {
        var gestor = await new OpcionesFormularioNovedad(new NovedadConsultasFalsas(), UsuarioNovedad.Gestor()).ObtenerAsync(Ct);
        var admin = await new OpcionesFormularioNovedad(new NovedadConsultasFalsas(), UsuarioNovedad.Admin()).ObtenerAsync(Ct);

        Assert.False(gestor.PuedeRegistrarGeneral);
        Assert.Empty(gestor.TiposPorAplicacion["GENERAL"]);
        Assert.Equal(["CALAMIDAD_DOMESTICA", "PATERNIDAD", "PERMISO", "VACACIONES"], gestor.TiposPorAplicacion["PERSONA"].Select(t => t.Codigo));
        Assert.Equal(["FERIADO"], gestor.TiposPorAplicacion["PROYECTO"].Select(t => t.Codigo));
        Assert.True(admin.PuedeRegistrarGeneral);
        Assert.Equal(["FERIADO"], admin.TiposPorAplicacion["GENERAL"].Select(t => t.Codigo));
        Assert.Equal(366, admin.MaxDias);
    }
}
