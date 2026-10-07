import { Button, MessageBar, MessageBarBody, Text, makeStyles, tokens } from '@fluentui/react-components'
import { Add20Regular, ArrowDown20Regular, ArrowUp20Regular, Delete20Regular } from '@fluentui/react-icons'
import type { Dispatch } from 'react'
import {
  type AccionFormulario,
  type CambiosPrincipal,
  type ErroresFila,
  type FilaPrincipal,
  mensajesSinCampo,
} from '../formularioProyecto'
import type { JornadaCatalogo } from '../tipos'
import { AyudaAgregarPersonal } from './AyudaAgregarPersonal'
import { CAMPOS_VISIBLES_PRINCIPAL } from '../camposVisibles'
import { CamposPrincipal } from './CamposPersona'
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
  /** Error de la lista completa (p. ej. máximo superado). */
  errorSeccion?: string
  /** Ayuda neutra bajo la sección (P6: mínimo de principales). */
  ayuda?: string
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
  errorSeccion,
  ayuda,
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

      {errorSeccion && (
        <MessageBar intent="error">
          <MessageBarBody>{errorSeccion}</MessageBarBody>
        </MessageBar>
      )}

      {principales.length === 0 && <Text size={200}>Sin principales.</Text>}
      {ayuda && <Text size={200}>{ayuda}</Text>}

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
  const actualizar = (cambios: CambiosPrincipal) => dispatch({ tipo: 'actualizarPrincipal', clave: fila.clave, cambios })
  const etiqueta = `P${numero}`

  return (
    <TarjetaPersona
      etiqueta={etiqueta}
      empleado={fila.empleado}
      advertencias={advertencia ? [advertencia] : []}
      errores={mensajesSinCampo(errores, CAMPOS_VISIBLES_PRINCIPAL)}
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
      <CamposPrincipal
        valores={fila}
        onCambiar={actualizar}
        jornadas={jornadas}
        errores={errores}
        cargoEmpleado={fila.empleado.cargo}
      />
    </TarjetaPersona>
  )
}
