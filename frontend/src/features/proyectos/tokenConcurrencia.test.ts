import { describe, expect, it } from 'vitest'
import { MENSAJE_CAMBIO_POR_OTRO, cambioPorOtro, conToken, versionDeEtapas } from './tokenConcurrencia'

describe('token base (TAREA-19b2)', () => {
  it('versionDeEtapas: la mayor versión, 0 sin etapas', () => {
    expect(versionDeEtapas([])).toBe(0)
    expect(versionDeEtapas([{ version: 1 }, { version: 4 }, { version: 3 }])).toBe(4)
  })

  it('cambioPorOtro: sin vista no; misma versión no; otra versión sí', () => {
    expect(cambioPorOtro(null, 5)).toBe(false)
    expect(cambioPorOtro({ datos: { versionProyecto: 5 } }, 5)).toBe(false)
    expect(cambioPorOtro({ datos: { versionProyecto: 6 } }, 5)).toBe(true)
  })

  it('conToken agrega la base sin tocar el resto del cuerpo', () => {
    expect(conToken({ fecha: '2026-11-24', versionProyecto: 9 }, 5)).toEqual({ fecha: '2026-11-24', versionProyecto: 5 })
  })

  it('mensaje del aviso', () => {
    expect(MENSAJE_CAMBIO_POR_OTRO).toBe('El proyecto cambió desde que abriste esta pantalla. Recarga los datos para continuar.')
  })
})
