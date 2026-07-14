import { useQuery } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/client'
import { asItems } from '../../shared/api/paging'
import type { Paged } from '../../shared/api/paging'

export interface Dashboard {
  generatedAt: string
  students: { enrolledTotal: number; enrolledToday: number }
  payments: { confirmedTotal: number; confirmedAmount: number; pendingTotal: number; pendingAmount: number }
  attendance: { recordsTotal: number; absencesToday: number }
  incidents: { reportedTotal: number; highSeverity: number }
  notifications: { sentTotal: number; failedTotal: number }
  events: { processedTotal: number; byType: Record<string, number> }
  failures: { failedMessages: number; dlqDepth: number }
  ecosystemStatus: string
}

export interface EventLogEntry {
  eventId: string
  eventType: string
  version: number
  occurredAt: string
  correlationId: string
  source: string
  entityId: string
  data: unknown
}

export interface Trace {
  correlationId: string
  totalEvents: number
  steps: EventLogEntry[]
}

export interface FailedMessage {
  eventId: string
  notificationId: string | null
  sourceEventType: string | null
  channel: string | null
  recipient: string | null
  attempts: number | null
  failureReason: string | null
  occurredAt: string
  correlationId: string | null
}

export interface Failures extends Paged<FailedMessage> {
  dlqDepth: number
}

export interface EcosystemStatus {
  generatedAt: string
  ecosystemStatus: string
  broker: string
  dlqDepth: number
  services: { name: string; status: string }[]
}

export interface EventFilters {
  type?: string
  correlationId?: string
}

// Indicadores consolidados con polling cada 10 segundos (contrato 13.5).
export function useDashboard() {
  return useQuery({
    queryKey: ['director', 'dashboard'],
    queryFn: async () => (await apiClient.get<Dashboard>('/api/analytics/dashboard')).data,
    refetchInterval: 10_000,
  })
}

export function useEventLog(filters: EventFilters, page: number) {
  return useQuery({
    queryKey: ['director', 'events', filters, page],
    queryFn: async () =>
      (
        await apiClient.get<Paged<EventLogEntry>>('/api/analytics/events', {
          params: {
            type: filters.type || undefined,
            correlationId: filters.correlationId || undefined,
            page,
          },
        })
      ).data,
    placeholderData: (previous) => previous,
  })
}

export function useTrace(correlationId: string | null) {
  return useQuery({
    queryKey: ['director', 'trace', correlationId],
    queryFn: async () =>
      (await apiClient.get<Trace>(`/api/analytics/events/${correlationId}/trace`)).data,
    enabled: correlationId !== null && correlationId.length > 0,
    retry: false,
  })
}

export function useFailures(page: number) {
  return useQuery({
    queryKey: ['director', 'failures', page],
    queryFn: async () =>
      (await apiClient.get<Failures>('/api/analytics/failures', { params: { page } })).data,
    placeholderData: (previous) => previous,
  })
}

export function useEcosystemStatus() {
  return useQuery({
    queryKey: ['director', 'ecosystem'],
    queryFn: async () =>
      (await apiClient.get<EcosystemStatus>('/api/analytics/ecosystem-status')).data,
    refetchInterval: 15_000,
  })
}

export interface NotificationItem {
  notificationId: string
  sourceEventType?: string
  channel?: string
  recipient?: string
  subject?: string
  status?: string
  attempts?: number
  sentAt?: string
  createdAt?: string
}

export function useNotifications(status: string) {
  return useQuery({
    queryKey: ['director', 'notifications', status],
    queryFn: async () =>
      asItems(
        (
          await apiClient.get<NotificationItem[] | Paged<NotificationItem>>('/api/notifications', {
            params: { status: status || undefined },
          })
        ).data,
      ),
    placeholderData: (previous) => previous,
  })
}
