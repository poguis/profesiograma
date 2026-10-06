import {
  Badge,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Dropdown,
  Field,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  MessageBarTitle,
  Option,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { useEffect, useMemo, useReducer, useRef, useState } from 'react'
import { Link, useBlocker } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { SelectorFecha } from '../../../components/SelectorFecha'
import { formatearFecha } from '../../../utils/formato'
import {
  CLAVE_ERROR,
  type ErroresCabecera,
  SIN_ERRORES_CABECERA,
  type ValoresCabecera,
  type VistaCabecera,
  aSolicitudCabecera,
  aSolicitudRegistroCabecera,
  crearEstadoEdicion,
  interpretarErrorCabecera,
  mensajeExitoCabecera,
  motivoSinRegistro,
  puedeRegistrar,
  puedeVerImpacto,
  rangoDesde,
  rangoFechaFin,
  rangoFechaInicio,
  reducerEdicion,
  requiereConfirmacion,
  validarEdicionCliente,
  vistaCabeceraVigente,
} from '../edicionCabecera'
import { useActividadesErp, useHorariosErp, useNombresCatalogo, usePrevisualizarCabecera, useRegistrarCabecera } from '../hooks'
import { textoHorario, textoRango } from '../textos'
import type { ActividadCabecera, CabeceraEdicion } from '../tipos'
import { ImpactoCabecera } from './ImpactoCabecera'

const useEstilos = makeStyles({
  superficie: { width: 'min(960px, 100vw)', maxWidth: '100vw', maxHeight: '100dvh' },
  contenido: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM, overflowY: 'auto' },
  grilla: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
    gap: tokens.spacingHorizontalM,
    alignItems: 'start',
  },
  seccion: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  tabla: { overflowX: 'auto' },
  secundario: { color: tokens.colorNeutralForeground3 },
  desactualizada: { opacity: 0.5 },
  acciones: { flexWrap: 'wrap', alignItems: 'center' },
})

/** Datos del detalle que el GET de cabecera no trae (catálogo de actividades del ERP). */
export interface ProyectoEdicionCabecera {
  id: number
  companiaId: number
  proyectoErpId: string | null
}

export interface DialogoEditarCabeceraProps {
  proyecto: ProyectoEdicionCabecera
  /** Cabecera con que se abre (puedeEditar = true). */
  cabecera: CabeceraEdicion
  /** Ruta del listado (enlace del 404). */
  rutaListado: string
  onCerrar: () => void
  /** Registro correcto: recibe el mensaje de éxito (P4). */
  onRealizado: (mensaje: string) => void
  /** Recarga detalle y cabecera (400 `proyecto` / 409) y devuelve la cabecera nueva. */
  onRecargar: () => Promise<CabeceraEdicion | undefined>
}

type Operacion = 'impacto' | 'registrar'

const SIN_ACTIVIDAD = ''

/** "Editar datos generales" (TAREA-19a). Se monta al abrirse, así que cada apertura empieza con los datos guardados. */
export function DialogoEditarCabecera({
  proyecto,
  cabecera,
  rutaListado,
  onCerrar,
  onRealizado,
  onRecargar,
}: DialogoEditarCabeceraProps) {
  const estilos = useEstilos()
  const [base, setBase] = useState(cabecera)
  const [estado, despachar] = useReducer(reducerEdicion, cabecera, crearEstadoEdicion)
  const [vista, setVista] = useState<VistaCabecera | null>(null)
  const [errores, setErrores] = useState<ErroresCabecera>(SIN_ERRORES_CABECERA)
  const [ultimaOperacion, setUltimaOperacion] = useState<Operacion>('impacto')
  const [recargando, setRecargando] = useState(false)
  const [confirmarCierre, setConfirmarCierre] = useState(false)
  const [formato, setFormato] = useState<Partial<Record<'fechaInicio' | 'fechaFin' | 'actividadDesde', string>>>({})
  const previsualizar = usePrevisualizarCabecera(proyecto.id)
  const registrar = useRegistrarCabecera(proyecto.id)
  const horarios = useHorariosErp()
  const actividadEditable = base.permisos.actividadEditable && proyecto.proyectoErpId !== null
  const actividadesErp = useActividadesErp(
    actividadEditable ? proyecto.companiaId : null,
    actividadEditable ? proyecto.proyectoErpId : null,
  )
  const nombres = useNombresCatalogo()
  // Refs: se leen en el mismo instante del clic o de la navegación, antes del siguiente render.
  const enviandoRef = useRef(false)
  const registradoRef = useRef(false)

  const { valores } = estado
  const enviando = previsualizar.isPending || registrar.isPending || recargando
  const vigente = vistaCabeceraVigente(vista, estado.revision)
  const habilitadoRegistrar = puedeRegistrar(vista, estado, enviando)
  const confirmacion = vigente && requiereConfirmacion(vista!.datos)

  // El mensaje del servidor tiene prioridad sobre la ayuda del cliente.
  const ayudas = useMemo(() => validarEdicionCliente(valores), [valores])
  const mensajes = { ...ayudas, ...errores.campos }

  /** Cambiar un campo borra el error del servidor de ese campo. */
  const cambiar = (cambios: Partial<ValoresCabecera>) => {
    despachar({ tipo: 'campos', cambios })
    const claves = (Object.keys(cambios) as (keyof ValoresCabecera)[]).map((k) => CLAVE_ERROR[k])
    setErrores((actuales) => {
      const campos = { ...actuales.campos }
      claves.forEach((c) => delete campos[c])
      return { ...actuales, campos }
    })
  }

  // ---------------------------------------------------------------- confirmar al cerrar o salir con cambios (TAREA-13)
  const sucio = estado.editado
  const bloqueo = useBlocker(
    ({ currentLocation, nextLocation }) =>
      sucio && !registradoRef.current && currentLocation.pathname !== nextLocation.pathname,
  )

  useEffect(() => {
    if (!sucio) {
      return
    }
    const alSalir = (evento: BeforeUnloadEvent) => {
      if (!registradoRef.current) {
        evento.preventDefault()
      }
    }
    window.addEventListener('beforeunload', alSalir)
    return () => window.removeEventListener('beforeunload', alSalir)
  }, [sucio])

  const intentarCerrar = () => {
    if (enviando) {
      return
    }
    if (sucio) {
      setConfirmarCierre(true)
    } else {
      onCerrar()
    }
  }

  // ---------------------------------------------------------------- operaciones
  const registrarError = (error: unknown) =>
    setErrores(
      error instanceof ErrorApi
        ? interpretarErrorCabecera(error)
        : { ...SIN_ERRORES_CABECERA, generales: ['Ocurrió un error inesperado.'] },
    )

  const verImpacto = () => {
    if (!puedeVerImpacto(valores, enviando)) {
      return
    }
    const revision = estado.revision
    setUltimaOperacion('impacto')
    setErrores(SIN_ERRORES_CABECERA)
    previsualizar.mutate(aSolicitudCabecera(estado), {
      onSuccess: (datos) => setVista({ revision, datos }),
      onError: (error) => {
        setVista(null)
        registrarError(error)
      },
    })
  }

  const confirmarRegistro = () => {
    // El ref evita un doble envío aunque el segundo clic llegue antes de que se deshabilite el botón.
    if (enviandoRef.current || !puedeRegistrar(vista, estado, enviando)) {
      return
    }
    enviandoRef.current = true
    const tipoEtapa = vista!.datos.tipoEtapa
    setUltimaOperacion('registrar')
    setErrores(SIN_ERRORES_CABECERA)
    registrar.mutate(aSolicitudRegistroCabecera(estado, vista!), {
      onSuccess: (r) => {
        registradoRef.current = true // desactiva la confirmación al salir
        onRealizado(mensajeExitoCabecera(tipoEtapa, r.version))
      },
      onError: registrarError,
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  /** 400 `proyecto` / 409: recargar y empezar de nuevo con los datos actuales. */
  const recargar = async () => {
    setRecargando(true)
    let nueva: CabeceraEdicion | undefined
    try {
      nueva = await onRecargar()
    } finally {
      setRecargando(false)
      const cabeceraActual = nueva ?? base
      setBase(cabeceraActual)
      despachar({ tipo: 'reiniciar', cabecera: cabeceraActual })
      setVista(null)
      setErrores(SIN_ERRORES_CABECERA)
    }
  }

  // ---------------------------------------------------------------- textos derivados
  const rangoInicio = rangoFechaInicio(base)
  const rangoFin = rangoFechaFin(base, valores)
  const rangoActividad = rangoDesde(valores)
  const horarioElegido = horarios.data?.find((h) => h.codigo === valores.horarioCodigo)
  const horarioActualInactivo =
    horarios.data !== undefined &&
    base.horario.codigo !== null &&
    !horarios.data.some((h) => h.codigo === base.horario.codigo)
  const textoHorarioElegido = horarioElegido
    ? textoHorario(horarioElegido)
    : valores.horarioCodigo !== null && valores.horarioCodigo === base.horario.codigo
      ? textoHorario({ ...base.horario, descripcion: base.horario.descripcion ?? `Horario ${base.horario.codigo}` })
      : ''
  const actividadElegida = actividadesErp.data?.find((a) => a.id === valores.actividadId)
  const vigenteActual = base.actividadVigente

  return (
    <>
      <Dialog open onOpenChange={(_, d) => !d.open && intentarCerrar()}>
        <DialogSurface className={estilos.superficie}>
          <DialogBody>
            <DialogTitle>Editar datos generales · {base.codigo}</DialogTitle>
            <DialogContent className={estilos.contenido}>
              {/* Campos primero: el diálogo enfoca el primer elemento enfocable (el primer campo editable). */}
              <div className={estilos.grilla}>
                {base.permisos.fechaInicioEditable ? (
                  <Field
                    label="Fecha de inicio"
                    required
                    hint={`Desde ${formatearFecha(rangoInicio.minima)} (hoy).`}
                    validationMessage={formato.fechaInicio ?? mensajes.fechaInicio}
                  >
                    <SelectorFecha
                      valor={valores.fechaInicio ?? undefined}
                      fechaMinima={rangoInicio.minima}
                      onCambiar={(v) => cambiar({ fechaInicio: v ?? null })}
                      onErrorFormato={(m) => setFormato((f) => ({ ...f, fechaInicio: m }))}
                    />
                  </Field>
                ) : (
                  <Field label="Fecha de inicio" hint={base.permisos.motivoFechaInicio ?? undefined}>
                    <Text>{formatearFecha(base.fechaInicio)}</Text>
                  </Field>
                )}

                <Field
                  label="Fecha fin"
                  required
                  hint={`Desde ${formatearFecha(rangoFin.minima)}.`}
                  validationMessage={formato.fechaFin ?? mensajes.fechaFin}
                >
                  <SelectorFecha
                    valor={valores.fechaFin ?? undefined}
                    fechaMinima={rangoFin.minima}
                    onCambiar={(v) => cambiar({ fechaFin: v ?? null })}
                    onErrorFormato={(m) => setFormato((f) => ({ ...f, fechaFin: m }))}
                  />
                </Field>

                <Field
                  label="Horario"
                  required
                  hint={horarioActualInactivo ? 'El horario actual no está activo en el ERP; se conserva si no elige otro.' : undefined}
                  validationMessage={mensajes.horarioCodigo ?? mensajeCarga(horarios.error)}
                >
                  <Dropdown
                    placeholder={horarios.isPending ? 'Cargando…' : 'Seleccione el horario'}
                    disabled={horarios.isPending}
                    value={textoHorarioElegido}
                    selectedOptions={valores.horarioCodigo === null ? [] : [String(valores.horarioCodigo)]}
                    onOptionSelect={(_, d) => cambiar({ horarioCodigo: Number(d.optionValue) })}
                  >
                    {(horarios.data ?? []).map((h) => (
                      <Option key={h.codigo} value={String(h.codigo)} text={textoHorario(h)}>
                        {textoHorario(h)}
                      </Option>
                    ))}
                  </Dropdown>
                </Field>

                <Field label="Salida a almuerzo" required validationMessage={mensajes.salidaAlmuerzo}>
                  <Dropdown
                    placeholder="HH:mm"
                    value={valores.salidaAlmuerzo ?? ''}
                    selectedOptions={valores.salidaAlmuerzo ? [valores.salidaAlmuerzo] : []}
                    onOptionSelect={(_, d) => cambiar({ salidaAlmuerzo: d.optionValue ?? null })}
                  >
                    {base.opcionesAlmuerzo.salida.map((h) => (
                      <Option key={h} value={h}>
                        {h}
                      </Option>
                    ))}
                  </Dropdown>
                </Field>

                <Field label="Regreso de almuerzo" required validationMessage={mensajes.regresoAlmuerzo}>
                  <Dropdown
                    placeholder="HH:mm"
                    value={valores.regresoAlmuerzo ?? ''}
                    selectedOptions={valores.regresoAlmuerzo ? [valores.regresoAlmuerzo] : []}
                    onOptionSelect={(_, d) => cambiar({ regresoAlmuerzo: d.optionValue ?? null })}
                  >
                    {base.opcionesAlmuerzo.regreso.map((h) => (
                      <Option key={h} value={h}>
                        {h}
                      </Option>
                    ))}
                  </Dropdown>
                </Field>
              </div>

              <section className={estilos.seccion} aria-label="Actividad">
                <Text as="h3" weight="semibold" size={400}>
                  Actividad
                </Text>
                <Text>
                  Vigente hoy:{' '}
                  {vigenteActual
                    ? `${nombreActividad(vigenteActual)} (${textoRango(vigenteActual.fechaInicio, vigenteActual.fechaFin)})`
                    : 'Sin actividad vigente.'}
                </Text>

                {actividadEditable ? (
                  <div className={estilos.grilla}>
                    <Field
                      label="Cambiar a la actividad"
                      hint="Opcional. Deje «Sin cambio de actividad» para conservar las actuales."
                      validationMessage={mensajes['actividad.actividadId'] ?? mensajeCarga(actividadesErp.error)}
                    >
                      <Dropdown
                        placeholder={actividadesErp.isPending ? 'Cargando…' : 'Sin cambio de actividad'}
                        disabled={actividadesErp.isPending}
                        value={
                          actividadElegida
                            ? `${actividadElegida.id} · ${actividadElegida.descripcion}`
                            : (valores.actividadId ?? '')
                        }
                        selectedOptions={[valores.actividadId ?? SIN_ACTIVIDAD]}
                        onOptionSelect={(_, d) => {
                          const id = d.optionValue ?? SIN_ACTIVIDAD
                          // Sin actividad, "desde" tampoco aplica.
                          cambiar(id === SIN_ACTIVIDAD ? { actividadId: null, actividadDesde: null } : { actividadId: id })
                        }}
                      >
                        <Option value={SIN_ACTIVIDAD} text="Sin cambio de actividad">
                          Sin cambio de actividad
                        </Option>
                        {(actividadesErp.data ?? []).map((a) => (
                          <Option key={a.id} value={a.id} text={`${a.id} · ${a.descripcion}`}>
                            {`${a.id} · ${a.descripcion}`}
                          </Option>
                        ))}
                      </Dropdown>
                    </Field>

                    <Field
                      label="Aplica desde"
                      required={valores.actividadId !== null}
                      hint={
                        rangoActividad.minima && rangoActividad.maxima
                          ? `Entre ${formatearFecha(rangoActividad.minima)} y ${formatearFecha(rangoActividad.maxima)}.`
                          : undefined
                      }
                      validationMessage={formato.actividadDesde ?? mensajes['actividad.desde']}
                    >
                      <SelectorFecha
                        valor={valores.actividadDesde ?? undefined}
                        fechaMinima={rangoActividad.minima}
                        fechaMaxima={rangoActividad.maxima}
                        onCambiar={(v) => cambiar({ actividadDesde: v ?? null })}
                        onErrorFormato={(m) => setFormato((f) => ({ ...f, actividadDesde: m }))}
                      />
                    </Field>
                  </div>
                ) : (
                  <Text className={estilos.secundario}>El grupo {base.grupo} no usa actividad.</Text>
                )}

                {base.actividades.length > 0 && (
                  <div className={estilos.tabla}>
                    <Table aria-label="Historial de actividades" size="small">
                      <TableHeader>
                        <TableRow>
                          <TableHeaderCell>Versión</TableHeaderCell>
                          <TableHeaderCell>Actividad</TableHeaderCell>
                          <TableHeaderCell>Movimiento</TableHeaderCell>
                          <TableHeaderCell>Vigencia</TableHeaderCell>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {base.actividades.map((a) => (
                          <TableRow key={a.version}>
                            <TableCell>{a.version}</TableCell>
                            <TableCell>
                              {nombreActividad(a)}{' '}
                              {vigenteActual?.version === a.version && (
                                <Badge appearance="tint" color="success">
                                  Vigente
                                </Badge>
                              )}
                            </TableCell>
                            <TableCell>{nombres.movimiento(a.tipoMovimiento)}</TableCell>
                            <TableCell>{textoRango(a.fechaInicio, a.fechaFin)}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </section>

              {errores.generales.length > 0 && (
                <MessageBar intent="error">
                  <MessageBarBody>
                    {errores.generales.map((m) => (
                      <div key={m}>{m}</div>
                    ))}
                    {errores.noEncontrado && <Link to={rutaListado}>Ir al listado de proyectos</Link>}
                  </MessageBarBody>
                  {errores.ofrecerReintento && (
                    <MessageBarActions>
                      <Button disabled={enviando} onClick={ultimaOperacion === 'registrar' ? confirmarRegistro : verImpacto}>
                        Reintentar
                      </Button>
                    </MessageBarActions>
                  )}
                </MessageBar>
              )}

              {errores.ofrecerRecarga && (
                <MessageBar intent="warning">
                  <MessageBarBody>
                    <MessageBarTitle>Los datos del proyecto pueden haber cambiado.</MessageBarTitle>
                    Recargue para continuar con la información actual (se descartan los cambios del formulario).
                  </MessageBarBody>
                  <MessageBarActions>
                    <Button disabled={enviando} onClick={() => void recargar()}>
                      Recargar datos del proyecto
                    </Button>
                  </MessageBarActions>
                </MessageBar>
              )}

              <div>
                <Button disabled={!puedeVerImpacto(valores, enviando)} onClick={verImpacto}>
                  {previsualizar.isPending ? 'Calculando…' : 'Ver impacto'}
                </Button>
              </div>

              {vista !== null && !vigente && (
                <MessageBar intent="warning">
                  <MessageBarBody>
                    <MessageBarTitle>Vista previa desactualizada.</MessageBarTitle>
                    Cambiaron los datos. Pulse "Ver impacto" otra vez para poder registrar.
                  </MessageBarBody>
                </MessageBar>
              )}

              {vista !== null && (
                <div className={vigente ? undefined : estilos.desactualizada}>
                  <ImpactoCabecera datos={vista.datos} />
                </div>
              )}

              {confirmacion && (
                <Checkbox
                  checked={estado.entiende}
                  disabled={enviando}
                  label="Entiendo que esta acción no se puede deshacer."
                  onChange={(_, d) => despachar({ tipo: 'entiende', valor: d.checked === true })}
                />
              )}
            </DialogContent>

            <DialogActions className={estilos.acciones}>
              <Text size={200}>{motivoSinRegistro(vista, estado, enviando)}</Text>
              <Button disabled={enviando} onClick={intentarCerrar}>
                Cancelar
              </Button>
              <Button
                appearance="primary"
                icon={registrar.isPending ? <Spinner size="tiny" /> : undefined}
                disabled={!habilitadoRegistrar}
                onClick={confirmarRegistro}
              >
                {registrar.isPending ? 'Registrando…' : 'Registrar'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog
        modalType="alert"
        open={confirmarCierre || bloqueo.state === 'blocked'}
        onOpenChange={(_, d) => {
          if (!d.open) {
            setConfirmarCierre(false)
            bloqueo.reset?.()
          }
        }}
      >
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Cambios sin registrar</DialogTitle>
            <DialogContent>Hay cambios sin registrar. ¿Cerrar de todos modos?</DialogContent>
            <DialogActions>
              <Button
                onClick={() => {
                  setConfirmarCierre(false)
                  bloqueo.reset?.()
                }}
              >
                Seguir editando
              </Button>
              <Button
                appearance="primary"
                onClick={() => {
                  if (bloqueo.state === 'blocked') {
                    bloqueo.proceed?.()
                  } else {
                    onCerrar()
                  }
                }}
              >
                Cerrar sin registrar
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  )
}

function nombreActividad(a: ActividadCabecera) {
  return a.descripcion ? `${a.codigo} – ${a.descripcion}` : a.codigo
}

function mensajeCarga(error: Error | null): string | undefined {
  if (!error) {
    return undefined
  }
  return error instanceof ErrorApi ? `No se pudo cargar la lista: ${error.titulo}` : 'No se pudo cargar la lista.'
}
