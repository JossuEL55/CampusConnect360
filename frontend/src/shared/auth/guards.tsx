import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './auth-context'
import { canAccess, homeFor } from './roles'
import type { Role } from './roles'
import { Layout } from '../ui/Layout'

// Ruta de layout protegida: una sola verificación de sesión cubre todos los portales hijos.
export function AuthenticatedLayout() {
  const { user, isRestoring } = useAuth()
  const location = useLocation()

  if (isRestoring) return <div className="page-loader">Restaurando sesión…</div>
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />

  return (
    <Layout>
      <Outlet />
    </Layout>
  )
}

// Rol insuficiente: aviso sin cerrar sesión (contrato 13.6).
export function RequireRole({ allowed }: { allowed: Role[] }) {
  const { user } = useAuth()

  if (!canAccess(user, allowed)) {
    return (
      <section className="card state state-denied" role="alert">
        <h2>Acceso restringido</h2>
        <p>Tu rol no tiene permisos para este portal. Usa el menú para volver a tu área.</p>
      </section>
    )
  }
  return <Outlet />
}

export function HomeRedirect() {
  const { user } = useAuth()
  return <Navigate to={user ? homeFor(user.role) : '/login'} replace />
}
