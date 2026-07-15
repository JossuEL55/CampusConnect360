import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../../shared/auth/auth-context'
import { homeFor } from '../../shared/auth/roles'
import { useToast } from '../../shared/ui/toast'
import { Wordmark } from '../../shared/ui/icons'

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
    <div className="login-split">
      <aside className="brand-side" aria-hidden="true">
        <div className="brand-pattern" />
        <div className="brand-rings" />
        <div className="brand-top">
          <span className="wordmark-badge"><Wordmark size={21} /></span>
          <span className="brand-name">CampusConnect <span>360</span></span>
        </div>
        <div className="brand-msg">
          <div className="brand-eyebrow">Red de colegios · Ecuador</div>
          <h2 className="brand-headline">Una comunidad educativa, un solo panel.</h2>
          <p className="brand-sub">
            Matrícula, finanzas, asistencia y dirección conectadas en tiempo real.
            Información confiable para tomar mejores decisiones cada día.
          </p>
        </div>
        <div className="brand-foot">
          <div><b>4</b>colegios en red</div>
          <div><b>1.248</b>estudiantes</div>
          <div><b>99,9 %</b>disponibilidad</div>
        </div>
      </aside>

      <div className="form-side">
        <main className="login-card" aria-busy={isSubmitting}>
          {isSubmitting && <div className="login-progress" aria-hidden="true" />}
          <div className="login-brand">
            <span className="wordmark-badge"><Wordmark size={19} /></span>{' '}
            CampusConnect 360
          </div>
          <h1 className="login-title">Bienvenido de nuevo</h1>
          <p className="login-sub">Ingrese con su cuenta institucional para continuar.</p>
          <form onSubmit={onSubmit}>
            <div className="field">
              <label htmlFor="login-user">Usuario</label>
              <input
                id="login-user"
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                autoComplete="username"
                placeholder="nombre.apellido"
                required
                autoFocus
                disabled={isSubmitting}
              />
            </div>
            <div className="field">
              <label htmlFor="login-pass">Contraseña</label>
              <input
                id="login-pass"
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                autoComplete="current-password"
                placeholder="••••••••"
                required
                disabled={isSubmitting}
              />
            </div>
            <button type="submit" className="btn btn-block" disabled={isSubmitting}>
              {isSubmitting ? 'Conectando…' : 'Entrar'}
            </button>
          </form>
          <div className="login-foot">
            ¿Problemas para ingresar? Contacte a la mesa de ayuda de su colegio.
          </div>
        </main>
      </div>
    </div>
  )
}
