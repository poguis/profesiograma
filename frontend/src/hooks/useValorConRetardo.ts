import { useEffect, useState } from 'react'

/** Devuelve `valor` después de `esperaMs` sin cambios (debounce). Útil para búsquedas mientras se escribe. */
export function useValorConRetardo<T>(valor: T, esperaMs: number): T {
  const [retrasado, setRetrasado] = useState(valor)

  useEffect(() => {
    const temporizador = setTimeout(() => setRetrasado(valor), esperaMs)
    return () => clearTimeout(temporizador)
  }, [valor, esperaMs])

  return retrasado
}
