import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from '../shared/auth/auth-context'
import { AuthenticatedLayout, HomeRedirect, RequireRole } from '../shared/auth/guards'
import { Roles } from '../shared/auth/roles'
import { ToastProvider } from '../shared/ui/toast'
import { LoginPage } from '../features/login/LoginPage'
import { AcademicPortal } from '../features/academic/AcademicPortal'
import { FinancePortal } from '../features/finance/FinancePortal'
import { TeacherPortal } from '../features/teacher/TeacherPortal'
import { DirectorDashboard } from '../features/director/DirectorDashboard'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false },
  },
})

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <BrowserRouter>
          <AuthProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route element={<AuthenticatedLayout />}>
                <Route index element={<HomeRedirect />} />
                <Route element={<RequireRole allowed={[Roles.Academic]} />}>
                  <Route path="/academico" element={<AcademicPortal />} />
                </Route>
                <Route element={<RequireRole allowed={[Roles.Finance]} />}>
                  <Route path="/financiero" element={<FinancePortal />} />
                </Route>
                <Route element={<RequireRole allowed={[Roles.Teacher]} />}>
                  <Route path="/docente" element={<TeacherPortal />} />
                </Route>
                <Route element={<RequireRole allowed={[Roles.Director]} />}>
                  <Route path="/dashboard" element={<DirectorDashboard />} />
                </Route>
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </AuthProvider>
        </BrowserRouter>
      </ToastProvider>
    </QueryClientProvider>
  )
}
