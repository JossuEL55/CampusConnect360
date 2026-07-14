import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../../shared/auth/auth-context'
import { homeFor } from '../../shared/auth/roles'
import { useToast } from '../../shared/ui/toast'
import { Field } from '../../shared/ui/bits'

export function LoginPage() {
  const { user, login } = useAuth()
  const { notifyError } = useToast()
  const navigate = useNavigate()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [isSubmitting, setSubmitting] = useState(false)

  if (user) return <Navigate to={homeFor(user.role)} replace />

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    try {
      const logged = await login(username.trim(), password)
      navigate(homeFor(logged.role), { replace: true })
    } catch (error) {
      notifyError(error)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="login-page">
      <section className="login-hero">
        <h1>CampusConnect 360</h1>
        <p>
          Ecosistema de integración para la red de colegios: matrícula, pagos, asistencia,
          bienestar y analítica directiva en un solo lugar.
        </p>
      </section>
      <section className="login-panel">
        <form className="login-form" onSubmit={onSubmit}>
          <h2>Iniciar sesión</h2>
          <Field label="Usuario">
            <input
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              autoComplete="username"
              required
              autoFocus
            />
          </Field>
          <Field label="Contraseña">
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
              required
            />
          </Field>
          <button type="submit" className="btn" disabled={isSubmitting}>
            {isSubmitting ? 'Verificando…' : 'Entrar'}
          </button>
          <p className="login-users">
            Usuarios semilla: <code>secretaria</code> · <code>finanzas</code> ·{' '}
            <code>docente</code> · <code>direccion</code> · <code>admin</code>
          </p>
        </form>
      </section>
    </div>
  )
}
