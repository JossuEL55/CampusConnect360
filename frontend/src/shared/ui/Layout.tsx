import { NavLink } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from '../auth/auth-context'
import { canAccess, Roles } from '../auth/roles'

const NAV_ITEMS = [
  { to: '/academico', label: 'Académico', roles: [Roles.Academic] },
  { to: '/financiero', label: 'Financiero', roles: [Roles.Finance] },
  { to: '/docente', label: 'Docente', roles: [Roles.Teacher] },
  { to: '/dashboard', label: 'Dashboard', roles: [Roles.Director] },
]

export function Layout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth()

  return (
    <div className="shell">
      <header className="topbar">
        <span className="brand">
          <span className="brand-mark" aria-hidden="true">C360</span>
          CampusConnect
        </span>
        <nav aria-label="Portales">
          {NAV_ITEMS.filter((item) => canAccess(user, item.roles)).map((item) => (
            <NavLink key={item.to} to={item.to}>
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="session">
          <span className="session-name">{user?.fullName}</span>
          <span className="badge badge-brand">{user?.role}</span>
          <button type="button" className="btn btn-ghost" onClick={logout}>
            Salir
          </button>
        </div>
      </header>
      <main className="content">{children}</main>
    </div>
  )
}
