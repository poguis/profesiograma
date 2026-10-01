import { Button, Dropdown, Field, Input, Option, Text, makeStyles, tokens } from '@fluentui/react-components'
import { Add20Regular, ArrowDown20Regular, ArrowUp20Regular, Delete20Regular } from '@fluentui/react-icons'
import { type Dispatch, useState } from 'react'
import { SelectorFecha } from '../../../components/SelectorFecha'
import type { AccionFormulario, CambiosPrincipal, ErroresFila, FilaPrincipal } from '../formularioProyecto'
import type { JornadaCatalogo } from '../tipos'
import { AyudaAgregarPersonal } from './AyudaAgregarPersonal'
import { TarjetaPersona } from './TarjetaPersona'

const useEstilos = makeStyles({
  seccion: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  encabezado: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
})

export interface ListaPrincipalesProps {
  principales: FilaPrincipal[]
  dispatch: Dispatch<AccionFormulario>
  jornadas: JornadaCatalogo[]
  maximo: number
  /** El proyecto tiene fecha de inicio y fin (requisito para agregar personal). */
  rangoListo: boolean
  advertencias: Map<string, string>
  errores: Record<string, ErroresFila>
  onAgregar: () => void
}

export function ListaPrincipales({
  principales,
  dispatch,
  jornadas,
  maximo,
  rangoListo,
  advertencias,
  errores,
  onAgregar,
}: ListaPrincipalesProps) {
  const estilos = useEstilos()
  const lleno = principales.length >= maximo

  return (
    <section className={estilos.seccion} aria-label="Principales">
      <div className={estilos.encabezado}>
        <Text as="h3" size={500} weight="semibold">
          Principales ({principales.length} de {maximo})
        </Text>
        <Button icon={<Add20Regular />} disabled={!rangoListo || lleno} onClick={onAgregar}>
          Agregar principal
        </Button>
        <AyudaAgregarPersonal rangoListo={rangoListo} lleno={lleno} maximo={maximo} plural="principales" />
      </div>

      {principales.length === 0 && <Text size={200}>Sin principales (no son obligatorios).</Text>}

      {principales.map((p, i) => (
        <TarjetaPrincipal
          key={p.clave}
          fila={p}
          numero={i + 1}
          esPrimero={i === 0}
          esUltimo={i === principales.length - 1}
          dispatch={dispatch}
          jornadas={jornadas}
          advertencia={advertencias.get(p.clave)}
          errores={errores[p.clave] ?? {}}
        />
      ))}
    </section>
  )
}

interface TarjetaPrincipalProps {
  fila: FilaPrincipal
  numero: number
  esPrimero: boolean
  esUltimo: boolean
  dispatch: Dispatch<AccionFormulario>
  jornadas: JornadaCatalogo[]
  advertencia: string | undefined
  errores: ErroresFila
}

function TarjetaPrincipal({ fila, numero, esPrimero, esUltimo, dispatch, jornadas, advertencia, errores }: TarjetaPrincipalProps) {
  const [errorFormatoInicio, setErrorFormatoInicio] = useState<string>()
  const [errorFormatoFin, setErrorFormatoFin] = useState<string>()
  const actualizar = (cambios: CambiosPrincipal) => dispatch({ tipo: 'actualizarPrincipal', clave: fila.clave, cambios })
  const etiqueta = `P${numero}`
  const jornada = jornadas.find((j) => j.codigo === fila.jornada)

  return (
    <TarjetaPersona
      etiqueta={etiqueta}
      empleado={fila.empleado}
      advertencias={advertencia ? [advertencia] : []}
      acciones={
        <>
          <Button
            appearance="subtle"
            icon={<ArrowUp20Regular />}
            aria-label={`Subir ${etiqueta}`}
            title="Subir"
            disabled={esPrimero}
            onClick={() => dispatch({ tipo: 'moverPrincipal', clave: fila.clave, direccion: -1 })}
          />
          <Button
            appearance="subtle"
            icon={<ArrowDown20Regular />}
            aria-label={`Bajar ${etiqueta}`}
            title="Bajar"
            disabled={esUltimo}
            onClick={() => dispatch({ tipo: 'moverPrincipal', clave: fila.clave, direccion: 1 })}
          />
          <Button
            appearance="subtle"
            icon={<Delete20Regular />}
            aria-label={`Quitar ${etiqueta}`}
            title="Quitar"
            onClick={() => dispatch({ tipo: 'eliminarPrincipal', clave: fila.clave })}
          />
        </>
      }
    >
      <Field label="Jornada" required validationMessage={errores.jornada}>
        <Dropdown
          placeholder="Seleccione la jornada"
          value={jornada?.nombre ?? fila.jornada ?? ''}
          selectedOptions={fila.jornada ? [fila.jornada] : []}
          onOptionSelect={(_, d) => actualizar({ jornada: d.optionValue ?? null })}
        >
          {jornadas.map((j) => (
            <Option key={j.codigo} value={j.codigo}>
              {j.nombre}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Inicio" required validationMessage={errorFormatoInicio ?? errores.fechaInicio}>
        <SelectorFecha
          valor={fila.fechaInicio ?? undefined}
          onCambiar={(valor) => actualizar({ fechaInicio: valor ?? null })}
          onErrorFormato={setErrorFormatoInicio}
        />
      </Field>

      <Field label="Fin" required validationMessage={errorFormatoFin ?? errores.fechaFin}>
        <SelectorFecha
          valor={fila.fechaFin ?? undefined}
          onCambiar={(valor) => actualizar({ fechaFin: valor ?? null })}
          onErrorFormato={setErrorFormatoFin}
        />
      </Field>

      <Field label="Cargo" hint="Opcional: si se deja vacío se usa el del empleado." validationMessage={errores.cargo}>
        <Input
          value={fila.cargo}
          maxLength={200}
          placeholder={fila.empleado.cargo ?? 'Cargo del empleado'}
          onChange={(_, d) => actualizar({ cargo: d.value })}
        />
      </Field>
    </TarjetaPersona>
  )
}
