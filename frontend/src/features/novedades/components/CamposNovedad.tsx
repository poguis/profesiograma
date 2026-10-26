import { Dropdown, Field, Option, Text, Textarea, makeStyles, tokens } from '@fluentui/react-components'
import { useState } from 'react'
import { SelectorFecha } from '../../../components/SelectorFecha'
import { contarDias } from '../diasNovedad'
import { LARGO_OBSERVACION } from '../formularioNovedad'
import type { TipoNovedadResumen } from '../tipos'
import { EtiquetaTipoNovedad } from './EtiquetaTipoNovedad'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  fechas: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalM,
    [MOVIL]: { flexDirection: 'column' },
  },
  fecha: { minWidth: '200px' },
  secundario: { color: tokens.colorNeutralForeground3 },
})

export interface ValoresCamposNovedad {
  tipo?: string
  fechaInicio?: string
  fechaFin?: string
  observacion: string
}

export type CampoNovedad = 'tipoNovedad' | 'fechaInicio' | 'fechaFin' | 'observacion'

export interface CamposNovedadProps {
  valores: ValoresCamposNovedad
  /** Tipos elegibles para la aplicación (opciones-formulario, RN16). */
  tipos: TipoNovedadResumen[]
  /** Mensaje por campo (cliente o servidor). */
  errores: Partial<Record<CampoNovedad, string>>
  onCambiar: (cambios: Partial<ValoresCamposNovedad>) => void
  deshabilitado?: boolean
}

/**
 * Tipo, rango y observación de una novedad (TAREA-24b; la edición de la 24c agrega los días excluidos). Los días son todos
 * los calendario del rango, fines de semana incluidos (N6/N17).
 */
export function CamposNovedad({ valores, tipos, errores, onCambiar, deshabilitado }: CamposNovedadProps) {
  const estilos = useEstilos()
  const [formatoInicio, setFormatoInicio] = useState<string>()
  const [formatoFin, setFormatoFin] = useState<string>()
  const tipo = tipos.find((t) => t.codigo === valores.tipo)
  const dias = contarDias(valores.fechaInicio, valores.fechaFin)
  const largo = valores.observacion.trim().length

  return (
    <>
      <Field label="Tipo de novedad" required validationMessage={errores.tipoNovedad}>
        <Dropdown
          placeholder={tipos.length === 0 ? 'Elija primero la aplicación' : 'Elija el tipo'}
          disabled={deshabilitado || tipos.length === 0}
          value={tipo?.nombre ?? ''}
          selectedOptions={valores.tipo ? [valores.tipo] : []}
          onOptionSelect={(_, datos) => onCambiar({ tipo: datos.optionValue })}
        >
          {tipos.map((t) => (
            <Option key={t.codigo} value={t.codigo} text={t.nombre}>
              <EtiquetaTipoNovedad tipo={t} />
            </Option>
          ))}
        </Dropdown>
      </Field>

      <div className={estilos.fechas}>
        <Field label="Desde" required className={estilos.fecha} validationMessage={formatoInicio ?? errores.fechaInicio}>
          <SelectorFecha
            valor={valores.fechaInicio}
            onCambiar={(valor) => onCambiar({ fechaInicio: valor })}
            onErrorFormato={setFormatoInicio}
          />
        </Field>
        <Field label="Hasta" required className={estilos.fecha} validationMessage={formatoFin ?? errores.fechaFin}>
          <SelectorFecha
            valor={valores.fechaFin}
            fechaMinima={valores.fechaInicio}
            onCambiar={(valor) => onCambiar({ fechaFin: valor })}
            onErrorFormato={setFormatoFin}
          />
        </Field>
      </div>
      {dias > 0 && (
        <Text className={estilos.secundario}>
          {dias} {dias === 1 ? 'día' : 'días'} (fines de semana incluidos)
        </Text>
      )}

      <Field
        label="Observación (opcional)"
        validationMessage={errores.observacion}
        hint={`${largo}/${LARGO_OBSERVACION}`}
      >
        <Textarea
          resize="vertical"
          disabled={deshabilitado}
          value={valores.observacion}
          onChange={(_, datos) => onCambiar({ observacion: datos.value })}
        />
      </Field>
    </>
  )
}
