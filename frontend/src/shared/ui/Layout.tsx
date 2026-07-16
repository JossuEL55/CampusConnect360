import { NavLink, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from '../auth/auth-context'
import { canAccess } from '../auth/roles'
import { LogOutIcon, Wordmark } from './icons'
import { PORTALS, portalFor } from './portal-nav'
import { useSection } from './use-section'

function initials(fullName?: string): string {
  if (!fullName) return '?'
  const parts = fullName.trim().split(/\s+/)
  return (parts[0][0] + (parts[1]?.[0] ?? '')).toUpperCase()
}

export function Layout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth()
  const { pathname } = useLocation()
  const portal = portalFor(pathname)
  const [activeSection, goSection] = useSection(portal?.sections.map((s) => s.id) ?? [''])

  return (
    <div className="shell">
      <header className="topbar">
        <div className="topbar-inner">
          <span className="brand-top">
            <span className="wordmark-badge"><Wordmark /></span>
            <span className="brand-name">CampusConnect <span>360</span></span>
          </span>
          <nav className="topnav" aria-label="Portales">
            {PORTALS.filter((item) => canAccess(user, item.roles)).map((item) => (
              <NavLink key={item.path} to={item.path}>
                {item.label}
              </NavLink>
            ))}
          </nav>
          <div className="session">
            <span className="session-id">
              <span className="session-name">{user?.fullName}</span>
              <span className="session-role">{user?.role}</span>
            </span>
            <span className="avatar" aria-hidden="true">{initials(user?.fullName)}</span>
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
        </div>
      </header>

      <div className="shell-body">
        <nav className="subnav" aria-label={portal ? `Secciones de ${portal.label}` : 'Secciones'}>
          {portal && <div className="subnav-label">{portal.title}</div>}
          {portal?.sections.map((section) => (
            <button
              key={section.id}
              type="button"
              className={activeSection === section.id ? 'active' : undefined}
              onClick={() => goSection(section.id)}
            >
              {section.icon}
              {section.label}
            </button>
          ))}
        </nav>
        <main className="content">{children}</main>
      </div>
    </div>
  )
}
