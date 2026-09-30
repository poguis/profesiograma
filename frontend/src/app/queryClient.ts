import { QueryClient } from '@tanstack/react-query'
import { esErrorDefinitivo } from '../api/errores'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // 400/401/403/404 no se reintentan; errores de red o 5xx, hasta 2 veces.
      retry: (fallos, error) => !esErrorDefinitivo(error) && fallos < 2,
      refetchOnWindowFocus: false,
      staleTime: 30_000,
    },
  },
})
