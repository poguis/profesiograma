import { describe, expect, it } from 'vitest'
import { ErrorApi } from '../../api/errores'
import { interpretarErrorDialogo } from './erroresDialogo'

const opciones = { campos: ['fecha'] as const }

describe('interpretarErrorDialogo: 503 con detail (TAREA-22c)', () => {
  it('base caída: título y detail del servidor, con Reintentar', () => {
    const error = new ErrorApi({
      tipo: 'noDisponible',
      estado: 503,
      titulo: 'Base de datos no disponible',
      detalle: 'La base de datos no respondió y no se guardó ningún cambio. Intente nuevamente en un minuto.',
    })

    expect(interpretarErrorDialogo(error, opciones)).toEqual({
      campos: {},
      generales: [
        'Base de datos no disponible: La base de datos no respondió y no se guardó ningún cambio. Intente nuevamente en un minuto.',
      ],
      ofrecerRecarga: false,
      ofrecerReintento: true,
      noEncontrado: false,
    })
  })

  it('503 sin detail: solo el título (como antes)', () => {
    const r = interpretarErrorDialogo({ tipo: 'noDisponible', titulo: 'Registro ocupado' }, opciones)

    expect(r).toMatchObject({ generales: ['Registro ocupado'], ofrecerReintento: true })
  })

  it('red y conflicto no muestran el detail', () => {
    expect(interpretarErrorDialogo({ tipo: 'red', titulo: 'Sin red', detalle: 'x' }, opciones).generales).toEqual(['Sin red'])
    expect(interpretarErrorDialogo({ tipo: 'conflicto', titulo: 'Cambió', detalle: 'x' }, opciones).generales).toEqual(['Cambió'])
  })
})
