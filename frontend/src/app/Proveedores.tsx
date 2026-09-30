import { FluentProvider, webLightTheme } from '@fluentui/react-components'
import { QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { queryClient } from './queryClient'

export function Proveedores({ children }: { children: ReactNode }) {
  return (
    <FluentProvider theme={webLightTheme} style={{ height: '100%' }}>
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </FluentProvider>
  )
}
