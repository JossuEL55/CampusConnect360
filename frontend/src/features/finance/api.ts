import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/client'
import { asItems } from '../../shared/api/paging'
import type { Paged } from '../../shared/api/paging'

export interface DebtorStudent {
  studentId: string
  fullName?: string
  studentCode?: string
  grade?: string
  pendingAmount?: number
  pendingDebts?: number
}

export interface Debt {
  debtId: string
  studentId: string
  concept: string
  amount: number
  dueDate: string
  status: string
}

export interface Payment {
  paymentId: string
  debtId: string
  studentId: string
  amount: number
  status: string
  confirmedAt: string
}

export interface DebtInput {
  studentId: string
  concept: string
  amount: number
  dueDate: string
}

export interface ConfirmPaymentInput {
  debtId: string
  paymentMethod: string
  reference: string
  paidAmount: number
}

const keys = { all: ['finance'] as const }

export function useDebtorStudents() {
  return useQuery({
    queryKey: ['finance', 'students'],
    queryFn: async () =>
      asItems((await apiClient.get<DebtorStudent[] | Paged<DebtorStudent>>('/api/payments/students')).data),
  })
}

export function usePendingDebts() {
  return useQuery({
    queryKey: ['finance', 'debts', 'Pending'],
    queryFn: async () =>
      asItems(
        (await apiClient.get<Debt[] | Paged<Debt>>('/api/payments/debts', { params: { status: 'Pending' } })).data,
      ),
  })
}

export function useConfirmedPayments() {
  return useQuery({
    queryKey: ['finance', 'payments', 'Confirmed'],
    queryFn: async () =>
      asItems(
        (await apiClient.get<Payment[] | Paged<Payment>>('/api/payments', { params: { status: 'Confirmed' } })).data,
      ),
  })
}

export function useCreateDebt() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: DebtInput) => (await apiClient.post<Debt>('/api/payments/debts', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.all }),
  })
}

export function useConfirmPayment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ debtId, ...body }: ConfirmPaymentInput) =>
      (await apiClient.post<Payment>(`/api/payments/debts/${debtId}/confirm`, body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.all }),
  })
}
