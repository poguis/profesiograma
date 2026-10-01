import {
  Button,
  Dropdown,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Option,
  SpinButton,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { Add20Regular, Delete20Regular } from '@fluentui/react-icons'
import { type Dispatch, useState } from 'react'
import { SelectorFecha } from '../../../components/SelectorFecha'
import {
  type AccionFormulario,
  type CambiosBack,
  type ErroresFila,
  type FilaBack,
  type FilaPrincipal,
  mensajesSinCampo,
  numeroPrincipal,
} from '../formularioProyecto'
import type { TipoRegistroBack } from '../tipos'
import { AyudaAgregarPersonal } from './AyudaAgregarPersonal'
import { TarjetaPersona } from './TarjetaPersona'

const useEstilos = makeStyles({
  seccion: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  encabezado: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
})

const TIPOS_REGISTRO: { valor: TipoRegistroBack; nombre: string }[] = [
  { valor: 'JORNADA', nombre: 'Jornada' },
  { valor: 'DESCANSO', nombre: 'Descanso' },
]

/** Valor del Dropdown para "sin principal relacionado". */
const SIN_RELACION = ''

const AVISO_RELACION = 'El principal relacionado fue quitado del proyecto. Elija otro o deje "Sin relación".'

export interface ListaBacksProps {
  backs: FilaBack[]
  principales: FilaPrincipal[]
  dispatch: Dispatch<AccionFormulario>
  maximo: number
  /** BACK_MAX_DIAS_DESCANSO (el servidor también lo valida). */
  maxDiasDescanso: number
  rangoListo: boolean
  advertencias: Map<string, string>
  errores: Record<string, ErroresFila>
  /** Error de la lista completa (p. ej. máximo superado). */
  errorSeccion?: string
  onAgregar: () => void
}

export function ListaBacks({
  backs,
  principales,
  dispatch,
  maximo,
  maxDiasDescanso,
  rangoListo,
  advertencias,
  errores,
  errorSeccion,
  onAgregar,
}: ListaBacksProps) {
  const estilos = useEstilos()
  const lleno = backs.length >= maximo

  return (
    <section className={estilos.seccion} aria-label="Backs">
      <div className={estilos.encabezado}>
        <Text as="h3" size={500} weight="semibold">
          Backs ({backs.length} de {maximo})
        </Text>
        <Button icon={<Add20Regular />} disabled={!rangoListo || lleno} onClick={onAgregar}>
          Agregar back
        </Button>
        <AyudaAgregarPersonal rangoListo={rangoListo} lleno={lleno} maximo={maximo} plural="backs" />
      </div>

      {errorSeccion && (
        <MessageBar intent="error">
          <MessageBarBody>{errorSeccion}</MessageBarBody>
        </MessageBar>
      )}

      {backs.length === 0 && <Text size={200}>Sin backs.</Text>}

      {backs.map((b, i) => (
        <TarjetaBack
          key={b.clave}
          fila={b}
          numero={i + 1}
          principales={principales}
          dispatch={dispatch}
          maxDiasDescanso={maxDiasDescanso}
          advertencias={[b.avisoRelacion ? AVISO_RELACION : undefined, advertencias.get(b.clave)].filter(
            (m): m is string => m !== undefined,
          )}
          errores={errores[b.clave] ?? {}}
        />
      ))}
    </section>
  )
}

const CAMPOS_VISIBLES = ['tipoRegistro', 'fechaInicio', 'fechaFin', 'principalRelacionado', 'observacion']

interface TarjetaBackProps {
  fila: FilaBack
  numero: number
  principales: FilaPrincipal[]
  dispatch: Dispatch<AccionFormulario>
  maxDiasDescanso: number
  advertencias: string[]
  errores: ErroresFila
}

function TarjetaBack({ fila, numero, principales, dispatch, maxDiasDescanso, advertencias, errores }: TarjetaBackProps) {
  const [errorFormatoInicio, setErrorFormatoInicio] = useState<string>()
  const [errorFormatoFin, setErrorFormatoFin] = useState<string>()
  const actualizar = (cambios: CambiosBack) => dispatch({ tipo: 'actualizarBack', clave: fila.clave, cambios })
  const etiqueta = `Back ${numero}`
  const relacionado = numeroPrincipal(principales, fila.principalClave)
  const textoPrincipal = (p: FilaPrincipal, i: number) => `P${i + 1} · ${p.empleado.nombreCompleto}`

  return (
    <TarjetaPersona
      etiqueta={etiqueta}
      empleado={fila.empleado}
      advertencias={advertencias}
      errores={mensajesSinCampo(
        errores,
        fila.tipoRegistro === 'JORNADA' ? [...CAMPOS_VISIBLES, 'diasDescanso'] : CAMPOS_VISIBLES,
      )}
      acciones={
        <Button
          appearance="subtle"
          icon={<Delete20Regular />}
          aria-label={`Quitar ${etiqueta}`}
          title="Quitar"
          onClick={() => dispatch({ tipo: 'eliminarBack', clave: fila.clave })}
        />
      }
    >
      <Field label="Tipo de registro" required validationMessage={errores.tipoRegistro}>
        <Dropdown
          value={TIPOS_REGISTRO.find((t) => t.valor === fila.tipoRegistro)?.nombre ?? ''}
          selectedOptions={[fila.tipoRegistro]}
          onOptionSelect={(_, d) => actualizar({ tipoRegistro: d.optionValue as TipoRegistroBack })}
        >
          {TIPOS_REGISTRO.map((t) => (
            <Option key={t.valor} value={t.valor}>
              {t.nombre}
            </Option>
          ))}
        </Dropdown>
      </Field>

      {/* R8: los días de descanso posteriores solo aplican a JORNADA (DESCANSO envía 0). */}
      {fila.tipoRegistro === 'JORNADA' && (
        <Field
          label="Días de descanso posterior"
          hint={`De 0 a ${maxDiasDescanso}.`}
          validationMessage={errores.diasDescanso}
        >
          <SpinButton
            min={0}
            max={maxDiasDescanso}
            value={fila.diasDescanso}
            onChange={(_, d) => {
              const valor = d.value ?? Number.parseInt(d.displayValue ?? '', 10)
              if (Number.isInteger(valor)) {
                actualizar({ diasDescanso: Math.min(Math.max(valor, 0), maxDiasDescanso) })
              }
            }}
          />
        </Field>
      )}

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

      <Field label="Principal relacionado" validationMessage={errores.principalRelacionado}>
        <Dropdown
          value={
            relacionado === null ? 'Sin relación' : textoPrincipal(principales[relacionado - 1], relacionado - 1)
          }
          selectedOptions={[fila.principalClave ?? SIN_RELACION]}
          onOptionSelect={(_, d) => actualizar({ principalClave: d.optionValue || null })}
        >
          <Option value={SIN_RELACION}>Sin relación</Option>
          {principales.map((p, i) => (
            <Option key={p.clave} value={p.clave} text={textoPrincipal(p, i)}>
              {textoPrincipal(p, i)}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Observación" validationMessage={errores.observacion}>
        <Input
          value={fila.observacion}
          maxLength={500}
          placeholder="Opcional"
          onChange={(_, d) => actualizar({ observacion: d.value })}
        />
      </Field>
    </TarjetaPersona>
  )
}
