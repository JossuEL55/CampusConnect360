import type { ReactNode } from 'react'
import {
  AlertIcon,
  CalendarIcon,
  GridIcon,
  HistoryIcon,
  LogIcon,
  MoneyIcon,
  StudentsIcon,
} from './icons'
import { Roles } from '../auth/roles'
import type { Role } from '../auth/roles'

export interface PortalSection {
  id: string
  label: string
  icon: ReactNode
}

export interface PortalDef {
  path: string
  label: string
  title: string
  roles: Role[]
  sections: PortalSection[]
}

// Cada portal declara sus secciones internas; la sidebar contextual las lista
// para el portal activo (no lista los portales, esos van en la topbar).
export const PORTALS: PortalDef[] = [
  {
    path: '/academico',
    label: 'Académico',
    title: 'Portal académico',
    roles: [Roles.Academic],
    sections: [
      { id: 'estudiantes', label: 'Estudiantes', icon: <StudentsIcon /> },
      { id: 'ficha', label: 'Ficha y matrícula', icon: <HistoryIcon /> },
    ],
  },
  {
    path: '/financiero',
    label: 'Financiero',
    title: 'Portal financiero',
    roles: [Roles.Finance],
    sections: [
      { id: 'deudas', label: 'Deudas y pagos', icon: <MoneyIcon /> },
      { id: 'estudiantes', label: 'Estudiantes', icon: <StudentsIcon /> },
    ],
  },
  {
    path: '/docente',
    label: 'Docente',
    title: 'Portal docente / bienestar',
    roles: [Roles.Teacher],
    sections: [
      { id: 'estudiantes', label: 'Mis estudiantes', icon: <StudentsIcon /> },
      { id: 'asistencia', label: 'Asistencia', icon: <CalendarIcon /> },
      { id: 'incidentes', label: 'Incidentes', icon: <AlertIcon /> },
    ],
  },
  {
    path: '/dashboard',
    label: 'Directivo',
    title: 'Dashboard directivo',
    roles: [Roles.Director],
    sections: [
      { id: 'indicadores', label: 'Vista general', icon: <GridIcon /> },
      { id: 'bitacora', label: 'Bitácora y detalle', icon: <LogIcon /> },
    ],
  },
]

export function portalFor(pathname: string): PortalDef | undefined {
  return PORTALS.find((portal) => portal.path === pathname)
}
