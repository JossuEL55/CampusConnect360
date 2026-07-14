import type { ReactNode } from 'react'
import { ApiError } from '../api/client'

export function Stat({ label, value, hint }: { label: string; value: ReactNode; hint?: string }) {
  return (
    <div className="stat">
      <span className="stat-value">{value}</span>
      <span className="stat-label">{label}</span>
      {hint && <span className="stat-hint">{hint}</span>}
    </div>
  )
}

type Tone = 'ok' | 'warn' | 'err' | 'muted' | 'brand'

export function Badge({ tone = 'muted', children }: { tone?: Tone; children: ReactNode }) {
  return <span className={`badge badge-${tone}`}>{children}</span>
}

export function statusTone(status: string): Tone {
  switch (status) {
    case 'Healthy':
    case 'Confirmed':
    case 'UpToDate':
    case 'NoDebt':
    case 'Sent':
    case 'Present':
      return 'ok'
    case 'Degraded':
    case 'Pending':
    case 'Late':
    case 'Justified':
      return 'warn'
    case 'Down':
    case 'Failed':
    case 'Overdue':
    case 'Absent':
    case 'High':
      return 'err'
    default:
      return 'muted'
  }
}

export function Loading({ label = 'Cargando…' }: { label?: string }) {
  return (
    <p className="state state-loading" role="status">
      {label}
    </p>
  )
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const isApi = error instanceof ApiError
  const message = isApi ? error.message : 'Ocurrió un error inesperado.'
  const traceId = isApi ? error.traceId : undefined
  const unavailable = isApi && (error.status === 404 || error.status >= 500 || error.status === 0)

  return (
    <div className="state state-error" role="alert">
      <p>{unavailable ? 'Este módulo aún no está disponible en el backend.' : message}</p>
      {traceId && <small>traceId: {traceId}</small>}
      {onRetry && (
        <button type="button" className="btn btn-ghost" onClick={onRetry}>
          Reintentar
        </button>
      )}
    </div>
  )
}

export function EmptyState({ message }: { message: string }) {
  return <p className="state state-empty">{message}</p>
}

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="field">
      <span className="field-label">{label}</span>
      {children}
    </label>
  )
}
