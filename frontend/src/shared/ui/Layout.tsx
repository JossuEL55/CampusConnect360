import { NavLink } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from '../auth/auth-context'
import { canAccess, Roles } from '../auth/roles'
import { LogOutIcon } from './icons'

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
          <span className="brand-mark" aria-hidden="true">C</span>{' '}
          CampusConnect 360
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
          <button
            type="button"
            className="icon-btn"
            onClick={logout}
            aria-label="Cerrar sesión"
            title="Cerrar sesión"
          >
            <LogOutIcon />
          </button>
        </div>
      </header>
      <main className="content">{children}</main>
    </div>
  )
}
