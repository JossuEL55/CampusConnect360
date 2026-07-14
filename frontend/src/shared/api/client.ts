import axios from 'axios'
import type { AxiosError } from 'axios'

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  traceId?: string
}

export class ApiError extends Error {
  readonly status: number
  readonly traceId?: string

  constructor(status: number, message: string, traceId?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.traceId = traceId
  }
}

const TOKEN_KEY = 'campus.token'
const USER_KEY = 'campus.user'
const CORRELATION_KEY = 'campus.correlation'

export const UNAUTHORIZED_EVENT = 'campus:unauthorized'

export function getToken(): string | null {
  return sessionStorage.getItem(TOKEN_KEY)
}

export function saveSession(token: string, user: unknown): void {
  sessionStorage.setItem(TOKEN_KEY, token)
  sessionStorage.setItem(USER_KEY, JSON.stringify(user))
}

export function getSavedUser<T>(): T | null {
  const raw = sessionStorage.getItem(USER_KEY)
  return raw ? (JSON.parse(raw) as T) : null
}

export function clearSession(): void {
  sessionStorage.removeItem(TOKEN_KEY)
  sessionStorage.removeItem(USER_KEY)
}

// Correlación única por sesión de usuario (contrato, sección 13).
export function getCorrelationId(): string {
  let id = sessionStorage.getItem(CORRELATION_KEY)
  if (!id) {
    id = `web-${crypto.randomUUID()}`
    sessionStorage.setItem(CORRELATION_KEY, id)
  }
  return id
}

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:8088',
})

apiClient.interceptors.request.use((config) => {
  const token = getToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  config.headers['X-Correlation-Id'] = getCorrelationId()
  return config
})

function messageFor(status: number, problem?: ProblemDetails): string {
  if (status === 401) return 'Tu sesión expiró. Inicia sesión nuevamente.'
  if (status === 403) return 'Tu rol no tiene permisos para esta acción.'
  if (status === 0) return 'No hay conexión con el Gateway.'
  return problem?.detail ?? problem?.title ?? `Error inesperado (${status}).`
}

// Manejo de errores del contrato (13.6): 401 limpia sesión, resto se normaliza a ApiError.
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ProblemDetails>) => {
    const status = error.response?.status ?? 0
    if (status === 401) {
      clearSession()
      window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
    }
    const problem = error.response?.data
    return Promise.reject(new ApiError(status, messageFor(status, problem), problem?.traceId))
  },
)
