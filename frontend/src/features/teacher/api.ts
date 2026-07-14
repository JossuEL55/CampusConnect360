import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/client'
import { asItems } from '../../shared/api/paging'
import type { Paged } from '../../shared/api/paging'

// La réplica local identifica al estudiante con "id" (no "studentId").
export interface TeacherStudent {
  id: string
  fullName?: string
  studentCode?: string
  grade?: string
}

export interface AttendanceInput {
  studentId: string
  date: string
  status: 'Present' | 'Absent' | 'Late' | 'Justified'
  remarks: string
}

export interface IncidentInput {
  studentId: string
  type: 'Academic' | 'Disciplinary' | 'Wellbeing'
  severity: 'Low' | 'Medium' | 'High'
  description: string
  reportedBy: string
}

export interface HistoryEntry {
  date?: string
  occurredAt?: string
  status?: string
  type?: string
  severity?: string
  remarks?: string
  description?: string
}

const keys = { all: ['teacher'] as const }

export function useTeacherStudents(q: string, grade: string) {
  return useQuery({
    queryKey: ['teacher', 'students', q, grade],
    queryFn: async () =>
      asItems(
        (
          await apiClient.get<TeacherStudent[] | Paged<TeacherStudent>>('/api/attendance/students', {
            params: { q: q || undefined, grade: grade || undefined },
          })
        ).data,
      ),
    placeholderData: (previous) => previous,
  })
}

export function useStudentHistory(studentId: string | null) {
  return useQuery({
    queryKey: ['teacher', 'history', studentId],
    queryFn: async () =>
      asItems(
        (await apiClient.get<HistoryEntry[] | Paged<HistoryEntry>>(`/api/attendance/students/${studentId}/history`)).data,
      ),
    enabled: studentId !== null,
    retry: false,
  })
}

export function useRecordAttendance() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: AttendanceInput) => (await apiClient.post('/api/attendance/records', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.all }),
  })
}

export function useReportIncident() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: IncidentInput) => (await apiClient.post('/api/attendance/incidents', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.all }),
  })
}
