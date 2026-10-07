import { Dropdown, Field, Input, Option, SpinButton, Text } from '@fluentui/react-components'
import { useState } from 'react'
import { SelectorFecha } from '../../../components/SelectorFecha'
import { formatearFecha } from '../../../utils/formato'
import type { ErroresFila } from '../formularioProyecto'
import type { JornadaCatalogo, TipoRegistroBack } from '../tipos'

// Campos de una persona, comunes a "Nuevo proyecto" (TAREA-13) y "Actualizar personal" (TAREA-19b). Solo
// presentación: cada pantalla decide qué cambia, qué está bloqueado y qué errores mostrar.

/** Campo de solo lectura (vigentes que ya empezaron). */
function SoloLectura({ label, valor, hint }: { label: string; valor: string; hint?: string }) {
  return (
    <Field label={label} hint={hint}>
      <Text>{valor || '—'}</Text>
    </Field>
  )
}

export interface ValoresPrincipal {
  jornada: string | null
  fechaInicio: string | null
  fechaFin: string | null
  cargo: string
}

export interface CamposPrincipalProps {
  valores: ValoresPrincipal
  onCambiar: (cambios: Partial<ValoresPrincipal>) => void
  jornadas: JornadaCatalogo[]
  errores: ErroresFila
  /** Placeholder del cargo (puesto del empleado). */
  cargoEmpleado?: string | null
  /** Campos de solo lectura (edición: permisos de la vigente). */
  bloqueados?: { jornada?: boolean; fechaInicio?: boolean }
  /** Límites de los selectores ("yyyy-MM-dd"). */
  fechaMinimaFin?: string
  fechaMaxima?: string
}

export function CamposPrincipal({
  valores,
  onCambiar,
  jornadas,
  errores,
  cargoEmpleado,
  bloqueados = {},
  fechaMinimaFin,
  fechaMaxima,
}: CamposPrincipalProps) {
  const [errorFormatoInicio, setErrorFormatoInicio] = useState<string>()
  const [errorFormatoFin, setErrorFormatoFin] = useState<string>()
  const jornada = jornadas.find((j) => j.codigo === valores.jornada)

  return (
    <>
      {bloqueados.jornada ? (
        <SoloLectura label="Jornada" valor={jornada?.nombre ?? valores.jornada ?? ''} />
      ) : (
        <Field label="Jornada" required validationMessage={errores.jornada}>
          <Dropdown
            placeholder="Seleccione la jornada"
            value={jornada?.nombre ?? valores.jornada ?? ''}
            selectedOptions={valores.jornada ? [valores.jornada] : []}
            onOptionSelect={(_, d) => onCambiar({ jornada: d.optionValue ?? null })}
          >
            {jornadas.map((j) => (
              <Option key={j.codigo} value={j.codigo}>
                {j.nombre}
              </Option>
            ))}
          </Dropdown>
        </Field>
      )}

      {bloqueados.fechaInicio ? (
        <SoloLectura label="Inicio" valor={formatearFecha(valores.fechaInicio)} hint="Ya empezó: no se puede cambiar." />
      ) : (
        <Field label="Inicio" required validationMessage={errorFormatoInicio ?? errores.fechaInicio}>
          <SelectorFecha
            valor={valores.fechaInicio ?? undefined}
            fechaMaxima={fechaMaxima}
            onCambiar={(valor) => onCambiar({ fechaInicio: valor ?? null })}
            onErrorFormato={setErrorFormatoInicio}
          />
        </Field>
      )}

      <Field
        label="Fin"
        required
        hint={fechaMinimaFin ? `Desde ${formatearFecha(fechaMinimaFin)}.` : undefined}
        validationMessage={errorFormatoFin ?? errores.fechaFin}
      >
        <SelectorFecha
          valor={valores.fechaFin ?? undefined}
          fechaMinima={fechaMinimaFin}
          fechaMaxima={fechaMaxima}
          onCambiar={(valor) => onCambiar({ fechaFin: valor ?? null })}
          onErrorFormato={setErrorFormatoFin}
        />
      </Field>

      <Field label="Cargo" hint="Opcional: si se deja vacío se usa el del empleado." validationMessage={errores.cargo}>
        <Input
          value={valores.cargo}
          maxLength={200}
          placeholder={cargoEmpleado ?? 'Cargo del empleado'}
          onChange={(_, d) => onCambiar({ cargo: d.value })}
        />
      </Field>
    </>
  )
}

export interface ValoresBack {
  tipoRegistro: TipoRegistroBack
  diasDescanso: number
  fechaInicio: string | null
  fechaFin: string | null
  /** Valor del Dropdown de relación ("" = "Sin relación"). */
  relacion: string
  observacion: string
}

export interface OpcionRelacionCampo {
  valor: string
  texto: string
}

const TIPOS_REGISTRO: { valor: TipoRegistroBack; nombre: string }[] = [
  { valor: 'JORNADA', nombre: 'Jornada' },
  { valor: 'DESCANSO', nombre: 'Descanso' },
]

/** Valor del Dropdown para "sin principal relacionado". */
export const VALOR_SIN_RELACION = ''

export interface CamposBackProps {
  valores: ValoresBack
  onCambiar: (cambios: Partial<ValoresBack>) => void
  opcionesRelacion: OpcionRelacionCampo[]
  /** BACK_MAX_DIAS_DESCANSO (el servidor también lo valida). */
  maxDiasDescanso: number
  errores: ErroresFila
  /** Mensaje del campo relación (creación: principalRelacionado; edición: principalClave / principalId). */
  errorRelacion?: string
  bloqueados?: { fechaInicio?: boolean }
  fechaMinimaFin?: string
  fechaMaxima?: string
}

export function CamposBack({
  valores,
  onCambiar,
  opcionesRelacion,
  maxDiasDescanso,
  errores,
  errorRelacion,
  bloqueados = {},
  fechaMinimaFin,
  fechaMaxima,
}: CamposBackProps) {
  const [errorFormatoInicio, setErrorFormatoInicio] = useState<string>()
  const [errorFormatoFin, setErrorFormatoFin] = useState<string>()
  const relacion = opcionesRelacion.find((o) => o.valor === valores.relacion)

  return (
    <>
      <Field label="Tipo de registro" required validationMessage={errores.tipoRegistro}>
        <Dropdown
          value={TIPOS_REGISTRO.find((t) => t.valor === valores.tipoRegistro)?.nombre ?? ''}
          selectedOptions={[valores.tipoRegistro]}
          onOptionSelect={(_, d) => onCambiar({ tipoRegistro: d.optionValue as TipoRegistroBack })}
        >
          {TIPOS_REGISTRO.map((t) => (
            <Option key={t.valor} value={t.valor}>
              {t.nombre}
            </Option>
          ))}
        </Dropdown>
      </Field>

      {/* R8: los días de descanso posteriores solo aplican a JORNADA (DESCANSO envía 0). */}
      {valores.tipoRegistro === 'JORNADA' && (
        <Field label="Días de descanso posterior" hint={`De 0 a ${maxDiasDescanso}.`} validationMessage={errores.diasDescanso}>
          <SpinButton
            min={0}
            max={maxDiasDescanso}
            value={valores.diasDescanso}
            onChange={(_, d) => {
              const valor = d.value ?? Number.parseInt(d.displayValue ?? '', 10)
              if (Number.isInteger(valor)) {
                onCambiar({ diasDescanso: Math.min(Math.max(valor, 0), maxDiasDescanso) })
              }
            }}
          />
        </Field>
      )}

      {bloqueados.fechaInicio ? (
        <SoloLectura label="Inicio" valor={formatearFecha(valores.fechaInicio)} hint="Ya empezó: no se puede cambiar." />
      ) : (
        <Field label="Inicio" required validationMessage={errorFormatoInicio ?? errores.fechaInicio}>
          <SelectorFecha
            valor={valores.fechaInicio ?? undefined}
            fechaMaxima={fechaMaxima}
            onCambiar={(valor) => onCambiar({ fechaInicio: valor ?? null })}
            onErrorFormato={setErrorFormatoInicio}
          />
        </Field>
      )}

      <Field
        label="Fin"
        required
        hint={fechaMinimaFin ? `Desde ${formatearFecha(fechaMinimaFin)}.` : undefined}
        validationMessage={errorFormatoFin ?? errores.fechaFin}
      >
        <SelectorFecha
          valor={valores.fechaFin ?? undefined}
          fechaMinima={fechaMinimaFin}
          fechaMaxima={fechaMaxima}
          onCambiar={(valor) => onCambiar({ fechaFin: valor ?? null })}
          onErrorFormato={setErrorFormatoFin}
        />
      </Field>

      <Field label="Principal relacionado" validationMessage={errorRelacion}>
        <Dropdown
          value={relacion?.texto ?? 'Sin relación'}
          selectedOptions={[valores.relacion]}
          onOptionSelect={(_, d) => onCambiar({ relacion: d.optionValue ?? VALOR_SIN_RELACION })}
        >
          <Option value={VALOR_SIN_RELACION}>Sin relación</Option>
          {opcionesRelacion.map((o) => (
            <Option key={o.valor} value={o.valor} text={o.texto}>
              {o.texto}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Observación" validationMessage={errores.observacion}>
        <Input
          value={valores.observacion}
          maxLength={500}
          placeholder="Opcional"
          onChange={(_, d) => onCambiar({ observacion: d.value })}
        />
      </Field>
    </>
  )
}
