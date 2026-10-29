import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Input,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { useCrearUsuario } from './api'
import {
  AVISO_CREADO,
  CAMPOS_ALTA,
  type CampoAlta,
  type ErroresUsuario,
  TEXTO_ROLES,
  interpretarErrorUsuario,
  normalizarAlta,
  validarAlta,
} from './usuariosAdmin'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  superficie: { [MOVIL]: { width: '100%', maxWidth: '100%' } },
  contenido: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  secundario: { color: tokens.colorNeutralForeground3 },
  acciones: { [MOVIL]: { flexDirection: 'column', '& > button': { width: '100%' } } },
})

/** Estado de navegación hacia el detalle (aviso de éxito). */
export interface EstadoNavegacionUsuario {
  aviso?: string
}

export function DialogoNuevoUsuario({ abierto, onCerrar }: { abierto: boolean; onCerrar: () => void }) {
  return (
    <Dialog open={abierto} onOpenChange={(_, d) => !d.open && onCerrar()}>
      {/* La clave cambia al abrir y al cerrar: cada apertura empieza vacía. */}
      <FormularioNuevoUsuario key={String(abierto)} onCerrar={onCerrar} />
    </Dialog>
  )
}

function FormularioNuevoUsuario({ onCerrar }: { onCerrar: () => void }) {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const crear = useCrearUsuario()
  const [email, setEmail] = useState('')
  const [nombre, setNombre] = useState('')
  const [campos, setCampos] = useState<Partial<Record<CampoAlta, string>>>({})
  const [errores, setErrores] = useState<ErroresUsuario<CampoAlta> | null>(null)
  const enviandoRef = useRef(false)

  const enviar = () => {
    if (enviandoRef.current || crear.isPending) {
      return
    }
    const solicitud = normalizarAlta(email, nombre)
    const locales = validarAlta(solicitud)
    setCampos(locales)
    setErrores(null)
    if (Object.keys(locales).length > 0) {
      return
    }
    enviandoRef.current = true
    crear.mutate(solicitud, {
      onSuccess: ({ id }) => {
        const estado: EstadoNavegacionUsuario = { aviso: AVISO_CREADO }
        void navigate(`/admin/usuarios/${id}`, { state: estado })
      },
      onError: (error) => {
        const r = interpretarErrorUsuario(error, CAMPOS_ALTA)
        setCampos(r.campos)
        setErrores(r)
      },
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  return (
    <DialogSurface className={estilos.superficie}>
      <form
        onSubmit={(evento) => {
          evento.preventDefault()
          enviar()
        }}
      >
        <DialogBody>
          <DialogTitle>Nuevo usuario</DialogTitle>
          <DialogContent className={estilos.contenido}>
            <Text className={estilos.secundario}>{TEXTO_ROLES}</Text>
            {errores && errores.generales.length > 0 && (
              <MessageBar intent="error">
                <MessageBarBody>
                  {errores.generales.map((m) => (
                    <div key={m}>{m}</div>
                  ))}
                </MessageBarBody>
                {(errores.usuarioExistenteId !== undefined || errores.ofrecerReintento) && (
                  <MessageBarActions>
                    {errores.usuarioExistenteId !== undefined && (
                      <Button onClick={() => void navigate(`/admin/usuarios/${errores.usuarioExistenteId}`)}>Abrir usuario</Button>
                    )}
                    {errores.ofrecerReintento && (
                      <Button disabled={crear.isPending} onClick={enviar}>
                        Reintentar
                      </Button>
                    )}
                  </MessageBarActions>
                )}
              </MessageBar>
            )}
            <Field label="Correo" required validationMessage={campos.email}>
              <Input type="email" value={email} disabled={crear.isPending} onChange={(_, d) => setEmail(d.value)} />
            </Field>
            <Field label="Nombre" required validationMessage={campos.nombreMostrar}>
              <Input value={nombre} disabled={crear.isPending} onChange={(_, d) => setNombre(d.value)} />
            </Field>
          </DialogContent>
          <DialogActions className={estilos.acciones}>
            <Button disabled={crear.isPending} onClick={onCerrar}>
              Cancelar
            </Button>
            <Button type="submit" appearance="primary" disabled={crear.isPending} icon={crear.isPending ? <Spinner size="tiny" /> : undefined}>
              {crear.isPending ? 'Guardando…' : 'Crear usuario'}
            </Button>
          </DialogActions>
        </DialogBody>
      </form>
    </DialogSurface>
  )
}
