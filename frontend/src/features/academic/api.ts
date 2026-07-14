import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/client'

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface Guardian {
  fullName: string
  email: string
  phone: string
}

export interface Student {
  studentId: string
  code: string
  identification: string
  firstName: string
  lastName: string
  birthDate: string
  grade: string
  schoolId: string
  guardian: Guardian
  status: string
  financialStatus: string
  createdAt: string
}

export interface StudentInput {
  identification: string
  firstName: string
  lastName: string
  birthDate: string
  grade: string
  schoolId: string
  guardian: Guardian
}

export interface Enrollment {
  enrollmentId: string
  studentId: string
  schoolYear: string
  grade: string
  schoolId: string
  status: string
  enrolledAt: string
}

export interface EnrollmentInput {
  studentId: string
  schoolYear: string
  grade: string
  schoolId: string
}

export interface AcademicEvent {
  eventId: string
  eventType: string
  correlationId: string
  payload: string
  occurredAt: string
}

const keys = {
  all: ['academic'] as const,
  students: (q: string, page: number) => ['academic', 'students', q, page] as const,
  student: (id: string) => ['academic', 'student', id] as const,
  enrollments: (id: string) => ['academic', 'enrollments', id] as const,
  events: (id: string) => ['academic', 'events', id] as const,
}

export function useStudents(q: string, page: number) {
  return useQuery({
    queryKey: keys.students(q, page),
    queryFn: async () =>
      (
        await apiClient.get<Paged<Student>>('/api/academic/students', {
          params: { q: q || undefined, page },
        })
      ).data,
    placeholderData: (previous) => previous,
  })
}

export function useStudent(id: string | null) {
  return useQuery({
    queryKey: keys.student(id ?? ''),
    queryFn: async () => (await apiClient.get<Student>(`/api/academic/students/${id}`)).data,
    enabled: id !== null,
  })
}

export function useEnrollments(id: string | null) {
  return useQuery({
    queryKey: keys.enrollments(id ?? ''),
    queryFn: async () =>
      (await apiClient.get<Enrollment[]>(`/api/academic/students/${id}/enrollments`)).data,
    enabled: id !== null,
  })
}

export function useStudentEvents(id: string | null) {
  return useQuery({
    queryKey: keys.events(id ?? ''),
    queryFn: async () =>
      (await apiClient.get<AcademicEvent[]>(`/api/academic/students/${id}/events`)).data,
    enabled: id !== null,
  })
}

export function useCreateStudent() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: StudentInput) =>
      (await apiClient.post<Student>('/api/academic/students', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.all }),
  })
}

export function useCreateEnrollment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: EnrollmentInput) =>
      (await apiClient.post<Enrollment>('/api/academic/enrollments', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.all }),
  })
}
