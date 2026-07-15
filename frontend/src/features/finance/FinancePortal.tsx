import { useState } from 'react'
import type { FormEvent } from 'react'
import { Badge, EmptyState, ErrorState, Field, Loading, PageHead, statusTone } from '../../shared/ui/bits'
import { useToast } from '../../shared/ui/toast'
import {
  useConfirmPayment,
  useConfirmedPayments,
  useCreateDebt,
  useDebtorStudents,
  usePendingDebts,
} from './api'
import type { Debt } from './api'

const money = new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' })

export function FinancePortal() {
  return (
    <>
      <PageHead kicker="Portal financiero" title="Deudas y confirmación de pagos">
        Registra obligaciones de pago, confirma transacciones y consulta el estado de deudas y pagos de la red.
      </PageHead>
      <div className="split">
        <div id="deudas" style={{ display: 'grid', gap: '1rem' }}>
          <CreateDebtSection />
          <PendingDebtsSection />
        </div>
        <div id="estudiantes" style={{ display: 'grid', gap: '1rem' }}>
          <DebtorStudentsSection />
          <ConfirmedPaymentsSection />
        </div>
      </div>
    </>
  )
}

function CreateDebtSection() {
  const { notify, notifyError } = useToast()
  const createDebt = useCreateDebt()
  const [form, setForm] = useState({ studentId: '', concept: '', amount: '', dueDate: '' })

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    try {
      await createDebt.mutateAsync({
        studentId: form.studentId.trim(),
        concept: form.concept.trim(),
        amount: Number(form.amount),
        dueDate: form.dueDate,
      })
      notify('success', 'Obligación de pago registrada.')
      setForm({ studentId: '', concept: '', amount: '', dueDate: '' })
    } catch (error) {
      notifyError(error)
    }
  }

  return (
    <section className="card">
      <h2>Registrar obligación de pago</h2>
      <form onSubmit={onSubmit} className="form-grid">
        <Field label="ID del estudiante">
          <input value={form.studentId} onChange={(e) => setForm({ ...form, studentId: e.target.value })} required />
        </Field>
        <Field label="Concepto">
          <input value={form.concept} onChange={(e) => setForm({ ...form, concept: e.target.value })} required placeholder="Matrícula 2026-2027" />
        </Field>
        <Field label="Monto (USD)">
          <input type="number" min="0.01" step="0.01" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} required />
        </Field>
        <Field label="Vencimiento">
          <input type="date" value={form.dueDate} onChange={(e) => setForm({ ...form, dueDate: e.target.value })} required />
        </Field>
        <button type="submit" className="btn" disabled={createDebt.isPending}>
          {createDebt.isPending ? 'Registrando…' : 'Registrar deuda'}
        </button>
      </form>
    </section>
  )
}

function DebtorStudentsSection() {
  const students = useDebtorStudents()

  return (
    <section className="card">
      <h2>Estudiantes matriculados</h2>
      {students.isPending && <Loading />}
      {students.isError && <ErrorState error={students.error} onRetry={() => students.refetch()} />}
      {students.data && students.data.length === 0 && <EmptyState message="Sin estudiantes con resumen de deuda." />}
      {students.data && students.data.length > 0 && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Estudiante</th><th>Grado</th><th>Deuda pendiente</th></tr>
            </thead>
            <tbody>
              {students.data.map((student) => (
                <tr key={student.studentId}>
                  <td>{student.fullName ?? student.studentCode ?? student.studentId}</td>
                  <td>{student.grade ?? '—'}</td>
                  <td>{student.pendingAmount !== undefined ? money.format(student.pendingAmount) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

function PendingDebtsSection() {
  const { notify, notifyError } = useToast()
  const debts = usePendingDebts()
  const confirmPayment = useConfirmPayment()
  const [selected, setSelected] = useState<Debt | null>(null)
  const [form, setForm] = useState({ paymentMethod: 'Transferencia', reference: '' })

  async function onConfirm(event: FormEvent) {
    event.preventDefault()
    if (!selected) return
    try {
      await confirmPayment.mutateAsync({
        debtId: selected.debtId,
        paymentMethod: form.paymentMethod,
        reference: form.reference.trim(),
        paidAmount: selected.amount,
      })
      notify('success', 'Pago confirmado. Se publicó el evento PaymentConfirmed.')
      setSelected(null)
      setForm({ paymentMethod: 'Transferencia', reference: '' })
    } catch (error) {
      notifyError(error)
    }
  }

  return (
    <section className="card">
      <h2>Deudas pendientes</h2>
      {debts.isPending && <Loading />}
      {debts.isError && <ErrorState error={debts.error} onRetry={() => debts.refetch()} />}
      {debts.data && debts.data.length === 0 && <EmptyState message="No hay deudas pendientes." />}
      {debts.data && debts.data.length > 0 && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Concepto</th><th>Monto</th><th>Vence</th><th></th></tr>
            </thead>
            <tbody>
              {debts.data.map((debt) => (
                <tr key={debt.debtId}>
                  <td>{debt.concept}</td>
                  <td>{money.format(debt.amount)}</td>
                  <td>{debt.dueDate}</td>
                  <td>
                    <button type="button" className="btn btn-ghost" onClick={() => setSelected(debt)}>
                      Confirmar pago
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {selected && (
        <form onSubmit={onConfirm} className="form-grid" style={{ marginTop: '0.75rem' }}>
          <Field label={`Método de pago (${selected.concept}, ${money.format(selected.amount)})`}>
            <select value={form.paymentMethod} onChange={(e) => setForm({ ...form, paymentMethod: e.target.value })}>
              <option>Transferencia</option>
              <option>Efectivo</option>
              <option>Tarjeta</option>
            </select>
          </Field>
          <Field label="Referencia">
            <input value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} required placeholder="BCO-778812" />
          </Field>
          <button type="submit" className="btn" disabled={confirmPayment.isPending}>
            {confirmPayment.isPending ? 'Confirmando…' : 'Confirmar'}
          </button>
        </form>
      )}
    </section>
  )
}

function ConfirmedPaymentsSection() {
  const payments = useConfirmedPayments()

  return (
    <section className="card">
      <h2>Pagos confirmados</h2>
      {payments.isPending && <Loading />}
      {payments.isError && <ErrorState error={payments.error} onRetry={() => payments.refetch()} />}
      {payments.data && payments.data.length === 0 && <EmptyState message="Aún no hay pagos confirmados." />}
      {payments.data && payments.data.length > 0 && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Pago</th><th>Monto</th><th>Estado</th><th>Fecha</th></tr>
            </thead>
            <tbody>
              {payments.data.map((payment) => (
                <tr key={payment.paymentId}>
                  <td><small>{payment.paymentId}</small></td>
                  <td>{money.format(payment.amount)}</td>
                  <td><Badge tone={statusTone(payment.status)}>{payment.status}</Badge></td>
                  <td>{new Date(payment.confirmedAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
