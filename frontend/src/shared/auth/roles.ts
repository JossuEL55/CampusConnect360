export const Roles = {
  Academic: 'Academic',
  Finance: 'Finance',
  Teacher: 'Teacher',
  Director: 'Director',
  Admin: 'Admin',
} as const

export type Role = (typeof Roles)[keyof typeof Roles]

export interface SessionUser {
  username: string
  fullName: string
  role: Role
}

// Admin accede a todos los portales (contrato, sección 4).
export function canAccess(user: SessionUser | null, allowed: Role[]): boolean {
  return user !== null && (user.role === Roles.Admin || allowed.includes(user.role))
}

export function homeFor(role: Role): string {
  switch (role) {
    case Roles.Academic:
      return '/academico'
    case Roles.Finance:
      return '/financiero'
    case Roles.Teacher:
      return '/docente'
    default:
      return '/dashboard'
  }
}
