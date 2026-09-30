import { Dropdown, Option, makeStyles, tokens } from '@fluentui/react-components'
import { useQueryClient } from '@tanstack/react-query'
import { useId, useState } from 'react'
import { USUARIOS_DEV, esUsuarioDev, guardarUsuarioDev, leerUsuarioDev, type UsuarioDev } from './devAuth'

const ETIQUETAS: Record<UsuarioDev, string> = {
  admin: 'admin (Admin)',
  gestor: 'gestor (Gestor)',
  anonimo: 'anonimo (sin identidad)',
}

const useEstilos = makeStyles({
  contenedor: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS },
  etiqueta: { color: tokens.colorNeutralForegroundOnBrand, whiteSpace: 'nowrap' },
  dropdown: { minWidth: '200px' },
})

/** Selector de usuario simulado. Solo se muestra en desarrollo (ver devAuthActivo). */
export function SelectorUsuarioDev() {
  const estilos = useEstilos()
  const idEtiqueta = useId()
  const queryClient = useQueryClient()
  const [usuario, setUsuario] = useState<UsuarioDev>(leerUsuarioDev)

  const cambiar = (valor: string | undefined) => {
    if (!esUsuarioDev(valor) || valor === usuario) {
      return
    }
    guardarUsuarioDev(valor)
    setUsuario(valor)
    // Los datos dependen del usuario: se descarta la caché y se vuelven a pedir las consultas activas.
    void queryClient.resetQueries()
  }

  return (
    <div className={estilos.contenedor}>
      <span id={idEtiqueta} className={estilos.etiqueta}>
        Usuario (dev):
      </span>
      <Dropdown
        aria-labelledby={idEtiqueta}
        className={estilos.dropdown}
        value={ETIQUETAS[usuario]}
        selectedOptions={[usuario]}
        onOptionSelect={(_, datos) => cambiar(datos.optionValue)}
      >
        {USUARIOS_DEV.map((u) => (
          <Option key={u} value={u}>
            {ETIQUETAS[u]}
          </Option>
        ))}
      </Dropdown>
    </div>
  )
}
