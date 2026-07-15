import { useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from '../../shared/auth/auth-context'
import { Badge, EmptyState, ErrorState, Field, Loading, PageHead, statusTone } from '../../shared/ui/bits'
import { useToast } from '../../shared/ui/toast'
import { useSection } from '../../shared/ui/use-section'
import { useRecordAttendance, useReportIncident, useStudentHistory, useTeacherStudents } from './api'

const today = () => new Date().toISOString().slice(0, 10)

const TITLES: Record<string, [string, string]> = {
  estudiantes: ['Mis estudiantes', 'Consulta a tus estudiantes y revisa su historial de asistencia e incidentes.'],
  asistencia: ['Registrar asistencia', 'Marca la asistencia diaria; las ausencias y atrasos generan notificación al representante.'],
  incidentes: ['Reportar incidente', 'Registra novedades de bienestar; severidad media o alta genera alerta al representante.'],
}

export function TeacherPortal() {
  const [section] = useSection(['estudiantes', 'asistencia', 'incidentes'])
  const [q, setQ] = useState('')
  const [grade, setGrade] = useState('')
  const students = useTeacherStudents(q, grade)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [title, subtitle] = TITLES[section]

  return (
    <>
      <PageHead kicker="Portal docente / bienestar" title={title}>{subtitle}</PageHead>

      {section === 'estudiantes' && (
        <section className="card">
          <h2>Estudiantes</h2>
          <div className="form-grid">
            <Field label="Buscar">
              <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Nombre o código" />
            </Field>
            <Field label="Grado">
              <input value={grade} onChange={(e) => setGrade(e.target.value)} placeholder="8vo EGB" />
            </Field>
          </div>
          {students.isPending && <Loading />}
          {students.isError && <ErrorState error={students.error} onRetry={() => students.refetch()} />}
          {students.data && students.data.length === 0 && (
            <EmptyState message="La réplica local de estudiantes aún no tiene datos." />
          )}
          {students.data && students.data.length > 0 && (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr><th>Estudiante</th><th>Grado</th></tr>
                </thead>
                <tbody>
                  {students.data.map((student) => (
                    <tr key={student.id} className="selectable" onClick={() => setSelectedId(student.id)}>
                      <td>{student.fullName ?? student.studentCode ?? student.id}</td>
                      <td>{student.grade ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {selectedId && <StudentHistory studentId={selectedId} />}
        </section>
      )}

      {section === 'asistencia' && <AttendanceForm studentId={selectedId} />}
      {section === 'incidentes' && <IncidentForm studentId={selectedId} />}
    </>
  )
}

function AttendanceForm({ studentId }: { studentId: string | null }) {
  const { notify, notifyError } = useToast()
  const record = useRecordAttendance()
  const [form, setForm] = useState({ studentId: '', date: today(), status: 'Present', remarks: '' })
  const effectiveStudentId = form.studentId || studentId || ''

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    try {
      await record.mutateAsync({
        studentId: effectiveStudentId,
        date: form.date,
        status: form.status as 'Present' | 'Absent' | 'Late' | 'Justified',
        remarks: form.remarks.trim(),
      })
      notify('success', 'Asistencia registrada.')
      setForm({ studentId: '', date: today(), status: 'Present', remarks: '' })
    } catch (error) {
      notifyError(error)
    }
  }

  return (
    <section className="card">
      <h2>Registrar asistencia</h2>
      <form onSubmit={onSubmit} className="form-grid">
        <Field label="ID del estudiante">
          <input
            value={effectiveStudentId}
            onChange={(e) => setForm({ ...form, studentId: e.target.value })}
            required
            placeholder="Selecciona de la lista o pega el UUID"
          />
        </Field>
        <Field label="Fecha">
          <input type="date" value={form.date} onChange={(e) => setForm({ ...form, date: e.target.value })} required />
        </Field>
        <Field label="Estado">
          <select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })}>
            <option value="Present">Presente</option>
            <option value="Absent">Ausente</option>
            <option value="Late">Atrasado</option>
            <option value="Justified">Justificado</option>
          </select>
        </Field>
        <Field label="Observaciones">
          <input value={form.remarks} onChange={(e) => setForm({ ...form, remarks: e.target.value })} />
        </Field>
        <button type="submit" className="btn" disabled={record.isPending}>
          {record.isPending ? 'Guardando…' : 'Registrar'}
        </button>
      </form>
    </section>
  )
}

function IncidentForm({ studentId }: { studentId: string | null }) {
  const { user } = useAuth()
  const { notify, notifyError } = useToast()
  const report = useReportIncident()
  const [form, setForm] = useState({ studentId: '', type: 'Wellbeing', severity: 'Medium', description: '' })
  const effectiveStudentId = form.studentId || studentId || ''

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    try {
      await report.mutateAsync({
        studentId: effectiveStudentId,
        type: form.type as 'Academic' | 'Disciplinary' | 'Wellbeing',
        severity: form.severity as 'Low' | 'Medium' | 'High',
        description: form.description.trim(),
        reportedBy: user?.username ?? 'docente',
      })
      notify('success', 'Incidente reportado.')
      setForm({ studentId: '', type: 'Wellbeing', severity: 'Medium', description: '' })
    } catch (error) {
      notifyError(error)
    }
  }

  return (
    <section className="card">
      <h2>Reportar incidente</h2>
      <form onSubmit={onSubmit} className="form-grid">
        <Field label="ID del estudiante">
          <input
            value={effectiveStudentId}
            onChange={(e) => setForm({ ...form, studentId: e.target.value })}
            required
          />
        </Field>
        <Field label="Tipo">
          <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}>
            <option value="Academic">Académico</option>
            <option value="Disciplinary">Disciplinario</option>
            <option value="Wellbeing">Bienestar</option>
          </select>
        </Field>
        <Field label="Severidad">
          <select value={form.severity} onChange={(e) => setForm({ ...form, severity: e.target.value })}>
            <option value="Low">Baja</option>
            <option value="Medium">Media</option>
            <option value="High">Alta</option>
          </select>
        </Field>
        <Field label="Descripción">
          <input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} required />
        </Field>
        <button type="submit" className="btn" disabled={report.isPending}>
          {report.isPending ? 'Reportando…' : 'Reportar'}
        </button>
      </form>
    </section>
  )
}

function StudentHistory({ studentId }: { studentId: string }) {
  const history = useStudentHistory(studentId)

  return (
    <>
      <h3 style={{ marginTop: '1rem' }}>Historial del estudiante</h3>
      {history.isFetching && <Loading />}
      {history.isError && <ErrorState error={history.error} />}
      {history.data && history.data.length === 0 && <EmptyState message="Sin registros para este estudiante." />}
      {history.data && history.data.length > 0 && (
        <ul className="timeline">
          {history.data.map((entry, index) => (
            <li key={index}>
              <Badge tone={statusTone(entry.status ?? entry.severity ?? '')}>
                {entry.status ?? entry.type ?? 'Registro'}
              </Badge>{' '}
              <time>{entry.date ?? entry.occurredAt ?? ''}</time>
              <div><small>{entry.remarks ?? entry.description ?? ''}</small></div>
            </li>
          ))}
        </ul>
      )}
    </>
  )
}
