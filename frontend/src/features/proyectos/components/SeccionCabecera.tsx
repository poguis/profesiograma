import { Dropdown, Field, Option, Text, makeStyles, tokens } from '@fluentui/react-components'
import { type Dispatch, useState } from 'react'
import { ErrorApi } from '../../../api/errores'
import { SelectorFecha } from '../../../components/SelectorFecha'
import type { AccionFormulario, CabeceraFormulario, CampoCabecera } from '../formularioProyecto'
import { textoHorario } from '../textos'
import { useActividadesErp, useDimensionesErp, useProyectosErp } from '../hooks'
import type { CompaniaErp, GrupoProyectoCatalogo, HorarioErp, OpcionesFormularioProyecto } from '../tipos'

const useEstilos = makeStyles({
  grilla: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
    gap: tokens.spacingHorizontalM,
    alignItems: 'start',
  },
  ancho: { gridColumn: '1 / -1' },
})

export interface SeccionCabeceraProps {
  cabecera: CabeceraFormulario
  dispatch: Dispatch<AccionFormulario>
  grupos: GrupoProyectoCatalogo[]
  companias: CompaniaErp[]
  horarios: HorarioErp[]
  opciones: OpcionesFormularioProyecto
  /** Mensaje por campo (ayudas del cliente y, en la B2, errores 400 del servidor). */
  errores: Partial<Record<CampoCabecera, string>>
}

export function SeccionCabecera({ cabecera, dispatch, grupos, companias, horarios, opciones, errores }: SeccionCabeceraProps) {
  const estilos = useEstilos()
  const { companiaId, grupo, proyectoErpId } = cabecera
  const usaErp = grupo?.requiereProyectoErp === true
  const usaDimension = grupo?.requiereDimension === true

  const proyectosErp = useProyectosErp(usaErp ? companiaId : null)
  const actividades = useActividadesErp(usaErp ? companiaId : null, usaErp ? proyectoErpId : null)
  const dimensiones = useDimensionesErp(usaDimension ? companiaId : null)

  const [errorFormatoInicio, setErrorFormatoInicio] = useState<string>()
  const [errorFormatoFin, setErrorFormatoFin] = useState<string>()

  const compania = companias.find((c) => c.id === companiaId)
  const proyectoErp = proyectosErp.data?.find((p) => p.id === proyectoErpId)
  const actividad = actividades.data?.find((a) => a.id === cabecera.actividadId)
  const dimension = dimensiones.data?.find((d) => d.uegpId === cabecera.dimensionUegpId)
  const horario = horarios.find((h) => h.codigo === cabecera.horarioCodigo)
  const departamentoUnico = opciones.departamentos.length === 1 ? opciones.departamentos[0] : undefined
  const departamento = opciones.departamentos.find((d) => d.id === cabecera.departamentoId)

  return (
    <div className={estilos.grilla}>
      <Field label="Compañía" required validationMessage={errores.companiaId}>
        <Dropdown
          placeholder="Seleccione la compañía"
          value={compania ? nombreCompania(compania) : ''}
          selectedOptions={companiaId === null ? [] : [String(companiaId)]}
          onOptionSelect={(_, d) => dispatch({ tipo: 'compania', companiaId: Number(d.optionValue) })}
        >
          {companias.map((c) => (
            <Option key={c.id} value={String(c.id)}>
              {nombreCompania(c)}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Grupo" required validationMessage={errores.grupo}>
        <Dropdown
          placeholder="Seleccione el grupo"
          value={grupos.find((g) => g.codigo === grupo?.codigo)?.nombre ?? ''}
          selectedOptions={grupo ? [grupo.codigo] : []}
          onOptionSelect={(_, d) => {
            const elegido = grupos.find((g) => g.codigo === d.optionValue)
            dispatch({
              tipo: 'grupo',
              grupo: elegido
                ? {
                    codigo: elegido.codigo,
                    requiereProyectoErp: elegido.requiereProyectoErp,
                    requiereDimension: elegido.requiereDimension,
                  }
                : null,
            })
          }}
        >
          {grupos.map((g) => (
            <Option key={g.codigo} value={g.codigo}>
              {g.nombre}
            </Option>
          ))}
        </Dropdown>
      </Field>

      {usaErp && (
        <>
          <Field
            label="Proyecto ERP"
            required
            validationMessage={errores.proyectoErpId ?? mensajeCarga(proyectosErp.error)}
            hint={companiaId === null ? 'Elija primero la compañía.' : undefined}
          >
            <Dropdown
              placeholder={proyectosErp.isFetching ? 'Cargando…' : 'Seleccione el proyecto ERP'}
              disabled={companiaId === null || proyectosErp.isPending}
              value={proyectoErp ? `${proyectoErp.id} · ${proyectoErp.nombre}` : (proyectoErpId ?? '')}
              selectedOptions={proyectoErpId ? [proyectoErpId] : []}
              onOptionSelect={(_, d) => dispatch({ tipo: 'proyectoErp', proyectoErpId: d.optionValue ?? null })}
            >
              {(proyectosErp.data ?? []).map((p) => (
                <Option key={p.id} value={p.id} text={`${p.id} · ${p.nombre}`}>
                  {`${p.id} · ${p.nombre}`}
                </Option>
              ))}
            </Dropdown>
          </Field>

          <Field
            label="Actividad"
            required
            validationMessage={errores.actividadId ?? mensajeCarga(actividades.error)}
            hint={proyectoErpId === null ? 'Elija primero el proyecto ERP.' : undefined}
          >
            <Dropdown
              placeholder={actividades.isFetching ? 'Cargando…' : 'Seleccione la actividad'}
              disabled={proyectoErpId === null || actividades.isPending}
              value={actividad ? `${actividad.id} · ${actividad.descripcion}` : (cabecera.actividadId ?? '')}
              selectedOptions={cabecera.actividadId ? [cabecera.actividadId] : []}
              onOptionSelect={(_, d) => dispatch({ tipo: 'cabecera', cambios: { actividadId: d.optionValue ?? null } })}
            >
              {(actividades.data ?? []).map((a) => (
                <Option key={a.id} value={a.id} text={`${a.id} · ${a.descripcion}`}>
                  {`${a.id} · ${a.descripcion}`}
                </Option>
              ))}
            </Dropdown>
          </Field>
        </>
      )}

      {usaDimension && (
        <Field
          label="Dimensión"
          required
          validationMessage={errores.dimensionUegpId ?? mensajeCarga(dimensiones.error)}
          hint={companiaId === null ? 'Elija primero la compañía.' : undefined}
        >
          <Dropdown
            placeholder={dimensiones.isFetching ? 'Cargando…' : 'Seleccione la dimensión'}
            disabled={companiaId === null || dimensiones.isPending}
            value={dimension ? `${dimension.uegpId} · ${dimension.descripcion}` : (cabecera.dimensionUegpId ?? '')}
            selectedOptions={cabecera.dimensionUegpId ? [cabecera.dimensionUegpId] : []}
            onOptionSelect={(_, d) => dispatch({ tipo: 'cabecera', cambios: { dimensionUegpId: d.optionValue ?? null } })}
          >
            {(dimensiones.data ?? []).map((d) => (
              <Option key={d.uegpId} value={d.uegpId} text={`${d.uegpId} · ${d.descripcion}`}>
                {`${d.uegpId} · ${d.descripcion}`}
              </Option>
            ))}
          </Dropdown>
        </Field>
      )}

      {grupo === null && (
        <Text className={estilos.ancho} size={200}>
          Elija el grupo para ver los datos del ERP que requiere (proyecto ERP y actividad, o dimensión).
        </Text>
      )}

      <Field label="Fecha de inicio" required validationMessage={errorFormatoInicio ?? errores.fechaInicio}>
        <SelectorFecha
          valor={cabecera.fechaInicio ?? undefined}
          onCambiar={(valor) => dispatch({ tipo: 'cabecera', cambios: { fechaInicio: valor ?? null } })}
          onErrorFormato={setErrorFormatoInicio}
        />
      </Field>

      <Field label="Fecha fin" required validationMessage={errorFormatoFin ?? errores.fechaFin}>
        <SelectorFecha
          valor={cabecera.fechaFin ?? undefined}
          onCambiar={(valor) => dispatch({ tipo: 'cabecera', cambios: { fechaFin: valor ?? null } })}
          onErrorFormato={setErrorFormatoFin}
        />
      </Field>

      <Field label="Horario" required validationMessage={errores.horarioCodigo}>
        <Dropdown
          placeholder="Seleccione el horario"
          value={horario ? textoHorario(horario) : ''}
          selectedOptions={cabecera.horarioCodigo === null ? [] : [String(cabecera.horarioCodigo)]}
          onOptionSelect={(_, d) => dispatch({ tipo: 'cabecera', cambios: { horarioCodigo: Number(d.optionValue) } })}
        >
          {horarios.map((h) => (
            <Option key={h.codigo} value={String(h.codigo)} text={textoHorario(h)}>
              {textoHorario(h)}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Salida a almuerzo" required validationMessage={errores.salidaAlmuerzo}>
        <Dropdown
          placeholder="HH:mm"
          value={cabecera.salidaAlmuerzo ?? ''}
          selectedOptions={cabecera.salidaAlmuerzo ? [cabecera.salidaAlmuerzo] : []}
          onOptionSelect={(_, d) => dispatch({ tipo: 'cabecera', cambios: { salidaAlmuerzo: d.optionValue ?? null } })}
        >
          {opciones.almuerzoSalidaOpciones.map((h) => (
            <Option key={h} value={h}>
              {h}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Regreso de almuerzo" required validationMessage={errores.regresoAlmuerzo}>
        <Dropdown
          placeholder="HH:mm"
          value={cabecera.regresoAlmuerzo ?? ''}
          selectedOptions={cabecera.regresoAlmuerzo ? [cabecera.regresoAlmuerzo] : []}
          onOptionSelect={(_, d) => dispatch({ tipo: 'cabecera', cambios: { regresoAlmuerzo: d.optionValue ?? null } })}
        >
          {opciones.almuerzoRegresoOpciones.map((h) => (
            <Option key={h} value={h}>
              {h}
            </Option>
          ))}
        </Dropdown>
      </Field>

      {/* P5: 0 departamentos → no se muestra; 1 → automático; varios → el usuario elige. */}
      {departamentoUnico && (
        <Field label="Departamento" validationMessage={errores.departamentoId}>
          <Text>{departamentoUnico.nombre}</Text>
        </Field>
      )}

      {opciones.departamentos.length > 1 && (
        <Field label="Departamento" required validationMessage={errores.departamentoId}>
          <Dropdown
            placeholder="Seleccione el departamento"
            value={departamento?.nombre ?? ''}
            selectedOptions={cabecera.departamentoId === null ? [] : [String(cabecera.departamentoId)]}
            onOptionSelect={(_, d) => dispatch({ tipo: 'cabecera', cambios: { departamentoId: Number(d.optionValue) } })}
          >
            {opciones.departamentos.map((d) => (
              <Option key={d.id} value={String(d.id)}>
                {d.nombre}
              </Option>
            ))}
          </Dropdown>
        </Field>
      )}
    </div>
  )
}

function nombreCompania(c: CompaniaErp) {
  return c.nombreCorto ?? c.nombre
}

function mensajeCarga(error: Error | null): string | undefined {
  if (!error) {
    return undefined
  }
  return error instanceof ErrorApi ? `No se pudo cargar la lista: ${error.titulo}` : 'No se pudo cargar la lista.'
}
