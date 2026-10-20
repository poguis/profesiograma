import { describe, expect, it } from 'vitest'
import {
  aConsultaApi,
  aParametrosUrl,
  abreviatura,
  aparienciaCelda,
  atenuar,
  contrasteTexto,
  esFinDeSemana,
  etiquetaCelda,
  enlaceCronogramaGeneral,
  estadoEnContexto,
  expandirDias,
  fechasAlCambiar,
  filtrarPersonas,
  indexarCeldas,
  inicialDia,
  leerFiltrosCronograma,
  mesAnterior,
  mesDe,
  mesSiguiente,
  mesesDelRango,
  normalizarTexto,
  opcionesProyecto,
  resumenCelda,
  textoAvisoRecorte,
  textoCelda,
} from './cronograma'
import type { CeldaCronograma, PersonaCronograma, ProyectoCronograma } from './tipos'

// Datos ficticios con la forma de CronogramaDto (no son de proyectos reales).
const proyecto = (id: number, nombre: string): ProyectoCronograma => ({
  id,
  codigo: `PRY-${id}`,
  nombre,
  estado: 'ACTIVO',
  grupo: 'CAMPO',
  companiaId: 9001,
  proyectoErpId: null,
  dimensionUegpId: null,
  horario: { codigo: 1, descripcion: null, entrada: '07:00:00', salida: '18:00:00', cruzaMedianoche: false },
  almuerzo: { salida: '13:00:00', regreso: '14:00:00' },
})
const PROYECTOS = new Map([
  [1, proyecto(1, 'TORRE NORTE')],
  [2, proyecto(2, 'PLANTA - ENSAMBLE')],
])
const celda = (cambios: Partial<CeldaCronograma>): CeldaCronograma => ({
  fecha: '2026-10-05',
  estado: 'PROYECTO',
  asignaciones: [{ proyectoId: 1, rol: 'PRINCIPAL', actividad: 'A.01' }],
  otrosProyectos: 0,
  novedad: null,
  novedadesExtra: 0,
  ...cambios,
})
const VACACIONES = {
  tipo: 'VACACIONES', sigla: 'V', color: '#0277BD', aplicacion: 'PERSONA' as const, origen: 'PROFESIOGRAMA',
  estadoReporte: 'LIBRE', observacionReporte: 'VACACIONES',
}
const persona = (codigoEkon: string, nombre: string): PersonaCronograma => ({ codigoEkon, nombre, asignadoEn: [], dias: [] })

describe('fechas', () => {
  it('expandirDias: un mes, cruce de mes y de año, un día, bisiesto y rango invertido', () => {
    expect(expandirDias('2026-10-01', '2026-10-31')).toHaveLength(31)
    expect(expandirDias('2026-12-30', '2027-01-02')).toEqual(['2026-12-30', '2026-12-31', '2027-01-01', '2027-01-02'])
    expect(expandirDias('2026-10-05', '2026-10-05')).toEqual(['2026-10-05'])
    expect(expandirDias('2028-02-28', '2028-03-01')).toEqual(['2028-02-28', '2028-02-29', '2028-03-01'])
    expect(expandirDias('2026-10-05', '2026-10-04')).toEqual([])
  })

  it('esFinDeSemana e inicialDia (martes y miércoles abreviados)', () => {
    expect([esFinDeSemana('2026-10-17'), esFinDeSemana('2026-10-18'), esFinDeSemana('2026-10-19')]).toEqual([true, true, false])
    expect(['2026-10-19', '2026-10-20', '2026-10-21', '2026-10-22', '2026-10-23', '2026-10-24', '2026-10-25'].map(inicialDia))
      .toEqual(['L', 'Mar', 'Mié', 'J', 'V', 'S', 'D'])
  })

  it('mesesDelRango: tramos por mes', () => {
    expect(mesesDelRango(expandirDias('2026-10-30', '2026-11-02'))).toEqual([
      { mes: 'octubre 2026', dias: 2 },
      { mes: 'noviembre 2026', dias: 2 },
    ])
  })

  it('mesDe, mesAnterior y mesSiguiente (cruce de año, febrero y 31 de marzo)', () => {
    expect(mesDe('2026-10-19')).toEqual({ desde: '2026-10-01', hasta: '2026-10-31' })
    expect(mesAnterior('2026-01-15')).toEqual({ desde: '2025-12-01', hasta: '2025-12-31' })
    expect(mesAnterior('2026-03-31')).toEqual({ desde: '2026-02-01', hasta: '2026-02-28' })
    expect(mesSiguiente('2026-12-01')).toEqual({ desde: '2027-01-01', hasta: '2027-01-31' })
    expect(mesSiguiente('2028-01-31')).toEqual({ desde: '2028-02-01', hasta: '2028-02-29' })
  })
})

describe('celdas y personas', () => {
  it('indexarCeldas: fecha → celda; las ausentes son LIBRE', () => {
    const indice = indexarCeldas([celda({ fecha: '2026-10-05' }), celda({ fecha: '2026-10-07' })])
    expect(indice.get('2026-10-07')?.fecha).toBe('2026-10-07')
    expect(indice.has('2026-10-06')).toBe(false)
  })

  it('normalizarTexto: sin tildes, minúsculas y espacios colapsados', () => {
    expect(normalizarTexto('  JOSÉ   Peña  ')).toBe('jose pena')
  })

  it('filtrarPersonas: por nombre sin tildes, por código y vacío = todas', () => {
    const personas = [persona('7255', 'JOSÉ PÉREZ'), persona('1001', 'ANA  LÓPEZ')]
    expect(filtrarPersonas(personas, 'jose').map((p) => p.codigoEkon)).toEqual(['7255'])
    expect(filtrarPersonas(personas, 'ana lopez').map((p) => p.codigoEkon)).toEqual(['1001'])
    expect(filtrarPersonas(personas, '725').map((p) => p.codigoEkon)).toEqual(['7255'])
    expect(filtrarPersonas(personas, '  ')).toHaveLength(2)
    expect(filtrarPersonas(personas, undefined)).toHaveLength(2)
  })

  it('abreviatura (P7): 2 caracteres, sin el prefijo "PLANTA - "', () => {
    expect(abreviatura('TORRE NORTE')).toBe('TO')
    expect(abreviatura('PLANTA - ENSAMBLE')).toBe('EN')
    expect(abreviatura('planta - ensamble')).toBe('EN')
    expect(abreviatura('X')).toBe('X')
  })

  it('textoCelda por estado', () => {
    expect(textoCelda(celda({ estado: 'NOVEDAD', novedad: VACACIONES }), PROYECTOS)).toEqual({ texto: 'V', extra: undefined })
    expect(textoCelda(celda({ estado: 'NOVEDAD', novedad: VACACIONES, novedadesExtra: 2 }), PROYECTOS)).toEqual({ texto: 'V', extra: '+2' })
    expect(textoCelda(celda({ estado: 'CHOQUE', otrosProyectos: 1 }), PROYECTOS).texto).toBe('C2')
    expect(textoCelda(celda({}), PROYECTOS).texto).toBe('TO')
    expect(textoCelda(celda({ estado: 'DESCANSO', asignaciones: [{ proyectoId: 2, rol: 'DESCANSO', actividad: null }] }), PROYECTOS).texto)
      .toBe('D-EN')
    expect(textoCelda(celda({ estado: 'OTRO_PROYECTO', asignaciones: [], otrosProyectos: 1 }), PROYECTOS).texto).toBe('')
  })
})

describe('colores y contraste', () => {
  it('contrasteTexto: texto oscuro sobre P/PM y blanco sobre el choque', () => {
    expect(contrasteTexto('#FFB900').color).toBe('#000000')
    expect(contrasteTexto('#201F1E').color).toBe('#FFFFFF')
    expect(contrasteTexto('#808080').contraste).toBeGreaterThanOrEqual(4.5)
  })

  it('atenuar: 35 % del color sobre blanco', () => {
    expect(atenuar('#000000', 0.35)).toBe('#A6A6A6')
    expect(atenuar('#FFFFFF', 0.35)).toBe('#FFFFFF')
  })

  it('aparienciaCelda por estado (P2 y P6)', () => {
    const colores = new Map([[1, '#3366AA']])
    expect(aparienciaCelda(celda({}), colores)).toMatchObject({ forma: 'solido', fondo: '#3366AA' })
    expect(aparienciaCelda(celda({ estado: 'NOVEDAD', novedad: VACACIONES }), colores)).toMatchObject({ forma: 'ficha', fondo: '#0277BD' })
    expect(aparienciaCelda(celda({ estado: 'DESCANSO', asignaciones: [{ proyectoId: 1, rol: 'DESCANSO', actividad: null }] }), colores))
      .toMatchObject({ forma: 'solido', fondo: atenuar('#3366AA', 0.35) })
    expect(aparienciaCelda(celda({ estado: 'CHOQUE' }), colores)).toMatchObject({ fondo: '#201F1E', texto: '#FFFFFF' })
    expect(aparienciaCelda(celda({ estado: 'OTRO_PROYECTO', asignaciones: [] }), colores).forma).toBe('rayado')
  })
})

describe('detalle', () => {
  it('resumenCelda: asignaciones, novedad con extras y otros proyectos sin sus datos (D2)', () => {
    const r = resumenCelda(celda({ estado: 'NOVEDAD', novedad: VACACIONES, novedadesExtra: 1, otrosProyectos: 1 }), PROYECTOS)
    expect(r).toEqual({
      fecha: '05/10/2026',
      estado: 'Novedad',
      asignaciones: ['PRY-1 – TORRE NORTE · PRINCIPAL · actividad A.01'],
      novedad: 'VACACIONES (de la persona, PROFESIOGRAMA) y 1 novedad(es) más',
      otros: 'Asignado además en 1 proyecto(s) de otros usuarios.',
    })
  })

  it('etiquetaCelda: aria-label con fecha y estado', () => {
    expect(etiquetaCelda(celda({ estado: 'OTRO_PROYECTO', asignaciones: [], otrosProyectos: 1 }), PROYECTOS))
      .toBe('05/10/2026. Otro proyecto. Asignado además en 1 proyecto(s) de otros usuarios.')
  })
})

describe('filtros ↔ URL', () => {
  it('ida y vuelta, sin vacíos; persona no va a la API', () => {
    const filtros = leerFiltrosCronograma(new URLSearchParams('desde=2026-10-01&hasta=2026-10-31&proyectoId=17&persona=jose&departamentoId='))
    expect(filtros).toEqual({ desde: '2026-10-01', hasta: '2026-10-31', proyectoId: 17, departamentoId: undefined, persona: 'jose' })
    expect(aParametrosUrl(filtros).toString()).toBe('desde=2026-10-01&hasta=2026-10-31&proyectoId=17&persona=jose')
    expect(aConsultaApi(filtros)).toBe('desde=2026-10-01&hasta=2026-10-31&proyectoId=17')
  })

  it('Id no válido en la URL se ignora', () => {
    expect(leerFiltrosCronograma(new URLSearchParams('proyectoId=abc&departamentoId=0')))
      .toMatchObject({ proyectoId: undefined, departamentoId: undefined })
  })

  it('opcionesProyecto (P8): los de la respuesta y el seleccionado aunque no venga', () => {
    expect(opcionesProyecto([...PROYECTOS.values()], 99).map((o) => o.id)).toEqual([99, 1, 2])
    expect(opcionesProyecto([...PROYECTOS.values()], 1).map((o) => o.id)).toEqual([1, 2])
  })
})

describe('aviso de recorte (D5 revisada)', () => {
  const base = { maxDias: 90, desde: '2026-10-01', hasta: '2026-12-29' }

  it('con recorte: rango efectivo y fecha solicitada en dd/MM/yyyy', () => {
    expect(textoAvisoRecorte({ ...base, recortado: true, hastaSolicitado: '2026-12-31' })).toBe(
      'El rango supera 90 días. Se muestra del 01/10/2026 al 29/12/2026 (solicitado hasta el 31/12/2026).',
    )
  })

  it('sin recorte: sin aviso', () => {
    expect(textoAvisoRecorte({ ...base, recortado: false, hastaSolicitado: null })).toBeUndefined()
  })

  it('maxDias distinto de 90', () => {
    expect(textoAvisoRecorte({ maxDias: 7, desde: '2026-10-01', hasta: '2026-10-07', recortado: true, hastaSolicitado: '2026-10-10' }))
      .toBe('El rango supera 7 días. Se muestra del 01/10/2026 al 07/10/2026 (solicitado hasta el 10/10/2026).')
  })
})

describe('fechasAlCambiar (el "desde" o el "hasta" que no se tocó se mantiene)', () => {
  const octubre = { desde: '2026-10-01', hasta: '2026-10-31', hastaSolicitado: null }
  const HOY = '2026-10-19'

  it('1. sin URL y respuesta de octubre: cambiar hasta al 30/11 conserva desde 01/10', () => {
    expect(fechasAlCambiar('hasta', '2026-11-30', {}, octubre, HOY)).toEqual({ desde: '2026-10-01', hasta: '2026-11-30' })
  })

  it('2. sin URL: cambiar desde al 15/10 conserva hasta 31/10', () => {
    expect(fechasAlCambiar('desde', '2026-10-15', {}, octubre, HOY)).toEqual({ desde: '2026-10-15', hasta: '2026-10-31' })
  })

  it('3. URL con las dos fechas: solo cambia la tocada', () => {
    const url = { desde: '2026-09-01', hasta: '2026-09-30' }
    expect(fechasAlCambiar('hasta', '2026-10-15', url, octubre, HOY)).toEqual({ desde: '2026-09-01', hasta: '2026-10-15' })
    expect(fechasAlCambiar('desde', '2026-09-10', url, octubre, HOY)).toEqual({ desde: '2026-09-10', hasta: '2026-09-30' })
  })

  it('4. con recorte (efectivo 29/12, solicitado 31/12): cambiar desde fija el hasta solicitado', () => {
    const recortado = { desde: '2026-10-01', hasta: '2026-12-29', hastaSolicitado: '2026-12-31' }
    expect(fechasAlCambiar('desde', '2026-10-05', {}, recortado, HOY)).toEqual({ desde: '2026-10-05', hasta: '2026-12-31' })
    // La URL tiene prioridad sobre la respuesta.
    expect(fechasAlCambiar('desde', '2026-10-05', { hasta: '2027-01-15' }, recortado, HOY).hasta).toBe('2027-01-15')
  })

  it('5. sin respuesta todavía: la otra fecha es la del mes actual en Ecuador', () => {
    expect(fechasAlCambiar('hasta', '2026-11-30', {}, undefined, HOY)).toEqual({ desde: '2026-10-01', hasta: '2026-11-30' })
    expect(fechasAlCambiar('desde', '2026-10-05', {}, undefined, HOY)).toEqual({ desde: '2026-10-05', hasta: '2026-10-31' })
  })

  it('accesos de mes: escriben las dos fechas', () => {
    expect(Object.keys(mesAnterior('2026-10-01'))).toEqual(['desde', 'hasta'])
    expect(Object.keys(mesSiguiente('2026-10-01'))).toEqual(['desde', 'hasta'])
  })
})

describe('pestaña del proyecto (TAREA-21b)', () => {
  it('estadoEnContexto (Q3): CHOQUE sin asignación del proyecto → OTRO_PROYECTO; con asignación, CHOQUE', () => {
    const choqueAjeno = celda({ estado: 'CHOQUE', asignaciones: [], otrosProyectos: 2 })
    const choquePropio = celda({ estado: 'CHOQUE', otrosProyectos: 1 })
    expect(estadoEnContexto(choqueAjeno, 1)).toBe('OTRO_PROYECTO')
    expect(estadoEnContexto(choquePropio, 1)).toBe('CHOQUE')
    expect(estadoEnContexto(choquePropio, 2)).toBe('OTRO_PROYECTO')
  })

  it('estadoEnContexto: sin proyecto (vista general) y los demás estados, sin cambios', () => {
    expect(estadoEnContexto(celda({ estado: 'CHOQUE', asignaciones: [] }))).toBe('CHOQUE')
    expect(estadoEnContexto(celda({}), 1)).toBe('PROYECTO')
    expect(estadoEnContexto(celda({ estado: 'DESCANSO' }), 1)).toBe('DESCANSO')
    expect(estadoEnContexto(celda({ estado: 'NOVEDAD', asignaciones: [], novedad: VACACIONES }), 1)).toBe('NOVEDAD')
    expect(estadoEnContexto(celda({ estado: 'OTRO_PROYECTO', asignaciones: [], otrosProyectos: 1 }), 1)).toBe('OTRO_PROYECTO')
  })

  it("resumenCelda y etiquetaCelda con contexto 'proyecto' (Q7)", () => {
    const c = celda({ estado: 'OTRO_PROYECTO', asignaciones: [], otrosProyectos: 2 })
    expect(resumenCelda(c, PROYECTOS, 'proyecto').otros).toBe('Asignado además en 2 otro(s) proyecto(s).')
    expect(etiquetaCelda(c, PROYECTOS, 'proyecto')).toBe('05/10/2026. Otro proyecto. Asignado además en 2 otro(s) proyecto(s).')
    expect(resumenCelda(c, PROYECTOS).otros).toBe('Asignado además en 2 proyecto(s) de otros usuarios.') // por defecto: 'general'
  })

  it('enlaceCronogramaGeneral (Q9): proyecto y rango efectivo', () => {
    expect(enlaceCronogramaGeneral(17, '2026-10-01', '2026-10-31')).toBe('/cronograma?desde=2026-10-01&hasta=2026-10-31&proyectoId=17')
  })

  it('fechasAlCambiar con el estado local de la pestaña: D12 recortado conserva el hasta solicitado', () => {
    const d12 = { desde: '2026-10-01', hasta: '2026-12-29', hastaSolicitado: '2027-03-15' }
    expect(fechasAlCambiar('desde', '2026-10-05', {}, d12, '2026-10-20')).toEqual({ desde: '2026-10-05', hasta: '2027-03-15' })
    expect(fechasAlCambiar('hasta', '2026-11-30', {}, d12, '2026-10-20')).toEqual({ desde: '2026-10-01', hasta: '2026-11-30' })
  })
})
