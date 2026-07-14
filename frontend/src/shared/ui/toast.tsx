import { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { ApiError } from '../api/client'

type ToastKind = 'info' | 'success' | 'error'

interface Toast {
  id: number
  kind: ToastKind
  message: string
  detail?: string
}

interface ToastContextValue {
  notify(kind: ToastKind, message: string, detail?: string): void
  notifyError(error: unknown): void
}

const ToastContext = createContext<ToastContextValue | null>(null)

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(1)

  const notify = useCallback((kind: ToastKind, message: string, detail?: string) => {
    const id = nextId.current++
    setToasts((current) => [...current, { id, kind, message, detail }])
    setTimeout(() => setToasts((current) => current.filter((toast) => toast.id !== id)), 6000)
  }, [])

  // 5xx y timeouts muestran el traceId de Problem Details para soporte (contrato 13.6).
  const notifyError = useCallback(
    (error: unknown) => {
      if (error instanceof ApiError) {
        notify('error', error.message, error.traceId ? `traceId: ${error.traceId}` : undefined)
      } else {
        notify('error', 'Ocurrió un error inesperado.')
      }
    },
    [notify],
  )

  const value = useMemo(() => ({ notify, notifyError }), [notify, notifyError])

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="toasts" aria-live="polite">
        {toasts.map((toast) => (
          <div key={toast.id} className={`toast toast-${toast.kind}`}>
            <p>{toast.message}</p>
            {toast.detail && <small>{toast.detail}</small>}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast debe usarse dentro de ToastProvider')
  return context
}
