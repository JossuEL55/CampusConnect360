import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../../shared/auth/auth-context'
import { homeFor } from '../../shared/auth/roles'
import { useToast } from '../../shared/ui/toast'

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
      setSubmitting(false)
    }
  }

  return (
    <div className="login-page">
      <main className="login-card" aria-busy={isSubmitting}>
        <div className="progress-slot">{isSubmitting && <div className="progress-bar" aria-hidden="true" />}</div>
        <div className="login-body">
          <span className="login-brand">
            <span className="brand-mark" aria-hidden="true">C</span>{' '}
            CampusConnect 360
          </span>
          <h1>Iniciar sesión</h1>
          <form onSubmit={onSubmit} className="login-form">
            <label className="field">
              <span className="field-label">Usuario</span>
              <input
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                autoComplete="username"
                required
                autoFocus
                disabled={isSubmitting}
              />
            </label>
            <label className="field">
              <span className="field-label">Contraseña</span>
              <input
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                autoComplete="current-password"
                required
                disabled={isSubmitting}
              />
            </label>
            <div className="login-actions">
              <button type="submit" className="btn" disabled={isSubmitting}>
                {isSubmitting ? 'Conectando…' : 'Entrar'}
              </button>
            </div>
          </form>
        </div>
      </main>
      <footer className="login-footer">
        Acceso exclusivo para personal autorizado de la red de colegios.
      </footer>
    </div>
  )
}
