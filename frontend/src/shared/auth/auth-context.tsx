import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import {
  apiClient,
  clearSession,
  getSavedUser,
  getToken,
  saveSession,
  UNAUTHORIZED_EVENT,
} from '../api/client'
import type { SessionUser } from './roles'

interface LoginResponse {
  accessToken: string
  expiresIn: number
  user: SessionUser
}

interface AuthContextValue {
  user: SessionUser | null
  isRestoring: boolean
  login(username: string, password: string): Promise<SessionUser>
  logout(): void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<SessionUser | null>(() => getSavedUser<SessionUser>())
  const [isRestoring, setIsRestoring] = useState(() => getToken() !== null)

  // Restaura la identidad al refrescar la página (contrato 13.1).
  useEffect(() => {
    if (!getToken()) {
      setIsRestoring(false)
      return
    }
    apiClient
      .get<SessionUser>('/api/auth/me')
      .then((response) => setUser(response.data))
      .catch(() => {
        clearSession()
        setUser(null)
      })
      .finally(() => setIsRestoring(false))
  }, [])

  useEffect(() => {
    const onUnauthorized = () => setUser(null)
    window.addEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
  }, [])

  const login = useCallback(async (username: string, password: string) => {
    const { data } = await apiClient.post<LoginResponse>('/api/auth/login', { username, password })
    saveSession(data.accessToken, data.user)
    setUser(data.user)
    return data.user
  }, [])

  const logout = useCallback(() => {
    clearSession()
    setUser(null)
  }, [])

  const value = useMemo(
    () => ({ user, isRestoring, login, logout }),
    [user, isRestoring, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth debe usarse dentro de AuthProvider')
  return context
}
