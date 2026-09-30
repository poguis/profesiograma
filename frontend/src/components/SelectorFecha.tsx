import { DatePicker, type DatePickerProps } from '@fluentui/react-datepicker-compat'
import { aFechaIso, desdeFechaEc, desdeFechaIso, formatearFecha } from '../utils/formato'

// Encapsula @fluentui/react-datepicker-compat: si el paquete cambia, solo se reemplaza este archivo.

const TEXTOS_CALENDARIO: NonNullable<DatePickerProps['strings']> = {
  months: [
    'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
    'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre',
  ],
  shortMonths: ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sept', 'oct', 'nov', 'dic'],
  days: ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'],
  shortDays: ['D', 'L', 'M', 'X', 'J', 'V', 'S'],
  goToToday: 'Ir a hoy',
  weekNumberFormatString: 'Semana {0}',
  prevMonthAriaLabel: 'Mes anterior',
  nextMonthAriaLabel: 'Mes siguiente',
  prevYearAriaLabel: 'Año anterior',
  nextYearAriaLabel: 'Año siguiente',
  prevYearRangeAriaLabel: 'Rango de años anterior',
  nextYearRangeAriaLabel: 'Rango de años siguiente',
  closeButtonAriaLabel: 'Cerrar calendario',
  selectedDateFormatString: 'Fecha seleccionada {0}',
  todayDateFormatString: 'Fecha de hoy {0}',
  monthPickerHeaderAriaLabel: '{0}, cambiar año',
  yearPickerHeaderAriaLabel: '{0}, cambiar mes',
  dayMarkedAriaLabel: 'marcado',
}

const LUNES = 1 // DayOfWeek.Monday (@fluentui/react-calendar-compat)

export interface SelectorFechaProps {
  /** Fecha en formato "yyyy-MM-dd" (el que usa la API); undefined = vacío. */
  valor: string | undefined
  /** Recibe "yyyy-MM-dd" o undefined al borrar. */
  onCambiar: (valor: string | undefined) => void
  /** Informa si el texto escrito no es una fecha dd/MM/yyyy válida (undefined = sin error). */
  onErrorFormato?: (mensaje: string | undefined) => void
  id?: string
  placeholder?: string
  'aria-labelledby'?: string
}

export function SelectorFecha({ valor, onCambiar, onErrorFormato, id, placeholder, ...aria }: SelectorFechaProps) {
  return (
    <DatePicker
      id={id}
      aria-labelledby={aria['aria-labelledby']}
      value={desdeFechaIso(valor)}
      onSelectDate={(fecha) => {
        onErrorFormato?.(undefined)
        onCambiar(fecha ? aFechaIso(fecha) : undefined)
      }}
      onValidationResult={({ error }) =>
        onErrorFormato?.(error === 'invalid-input' ? 'Ingrese una fecha válida con el formato dd/MM/yyyy.' : undefined)
      }
      formatDate={(fecha) => (fecha ? formatearFecha(aFechaIso(fecha)) : '')}
      parseDateFromString={desdeFechaEc}
      allowTextInput
      firstDayOfWeek={LUNES}
      strings={TEXTOS_CALENDARIO}
      showGoToToday
      placeholder={placeholder ?? 'dd/mm/aaaa'}
    />
  )
}
