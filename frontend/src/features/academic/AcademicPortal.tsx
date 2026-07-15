import { useState } from 'react'
import type { FormEvent } from 'react'
import { Badge, EmptyState, ErrorState, Field, Loading, PageHead, statusTone } from '../../shared/ui/bits'
import { useToast } from '../../shared/ui/toast'
import {
  useCreateEnrollment,
  useCreateStudent,
  useEnrollments,
  useStudent,
  useStudentEvents,
  useStudents,
} from './api'
import type { StudentInput } from './api'

const EMPTY_FORM: StudentInput = {
  identification: '',
  firstName: '',
  lastName: '',
  birthDate: '',
  grade: '',
  schoolId: 'SCH-001',
  guardian: { fullName: '', email: '', phone: '' },
}

export function AcademicPortal() {
  const [query, setQuery] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)

  const students = useStudents(search, page)
  const totalPages = students.data ? Math.max(1, Math.ceil(students.data.totalCount / students.data.pageSize)) : 1

  function onSearch(event: FormEvent) {
    event.preventDefault()
    setPage(1)
    setSearch(query.trim())
  }

  return (
    <>
      <PageHead kicker="Portal académico" title="Estudiantes y matrículas">
        Registra estudiantes, confirma matrículas y consulta la ficha con su estado académico y financiero.
      </PageHead>
      <div className="split">
        <section className="card" id="estudiantes">
          <div className="section-head">
            <h2>Estudiantes</h2>
            <button type="button" className="btn" onClick={() => setShowForm((v) => !v)}>
              {showForm ? 'Cerrar formulario' : 'Registrar estudiante'}
            </button>
          </div>

          {showForm && (
            <RegisterStudentForm
              onCreated={(id) => {
                setShowForm(false)
                setSelectedId(id)
              }}
            />
          )}

          <form onSubmit={onSearch} className="form-grid" role="search">
            <Field label="Buscar por nombre o cédula">
              <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Ej. Torres o 1712345678" />
            </Field>
            <button type="submit" className="btn btn-ghost">Buscar</button>
          </form>

          {students.isPending && <Loading label="Cargando estudiantes…" />}
          {students.isError && <ErrorState error={students.error} onRetry={() => students.refetch()} />}
          {students.data && students.data.items.length === 0 && (
            <EmptyState message="No hay estudiantes que coincidan con la búsqueda." />
          )}
          {students.data && students.data.items.length > 0 && (
            <>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Código</th>
                      <th>Nombre</th>
                      <th>Grado</th>
                      <th>Financiero</th>
                    </tr>
                  </thead>
                  <tbody>
                    {students.data.items.map((student) => (
                      <tr
                        key={student.studentId}
                        className="selectable"
                        onClick={() => setSelectedId(student.studentId)}
                      >
                        <td>{student.code}</td>
                        <td>{student.firstName} {student.lastName}</td>
                        <td>{student.grade}</td>
                        <td>
                          <Badge tone={statusTone(student.financialStatus)}>{student.financialStatus}</Badge>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="pager">
                <button type="button" className="btn btn-ghost" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                  Anterior
                </button>
                <span>Página {page} de {totalPages} · {students.data.totalCount} en total</span>
                <button type="button" className="btn btn-ghost" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
                  Siguiente
                </button>
              </div>
            </>
          )}
        </section>

        {selectedId ? (
          <StudentDetail studentId={selectedId} />
        ) : (
          <section className="card" id="ficha">
            <h2>Ficha del estudiante</h2>
            <EmptyState message="Selecciona un estudiante de la lista para ver su ficha, matrículas y eventos." />
          </section>
        )}
      </div>
    </>
  )
}

function RegisterStudentForm({ onCreated }: { onCreated: (studentId: string) => void }) {
  const { notify, notifyError } = useToast()
  const createStudent = useCreateStudent()
  const [form, setForm] = useState<StudentInput>(EMPTY_FORM)

  function set<K extends keyof StudentInput>(key: K, value: StudentInput[K]) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    try {
      const created = await createStudent.mutateAsync(form)
      notify('success', `Estudiante ${created.code} registrado.`)
      setForm(EMPTY_FORM)
      onCreated(created.studentId)
    } catch (error) {
      notifyError(error)
    }
  }

  return (
    <form onSubmit={onSubmit} style={{ marginBottom: '1rem' }}>
      <div className="form-grid">
        <Field label="Cédula">
          <input value={form.identification} onChange={(e) => set('identification', e.target.value)} required maxLength={20} />
        </Field>
        <Field label="Nombres">
          <input value={form.firstName} onChange={(e) => set('firstName', e.target.value)} required maxLength={80} />
        </Field>
        <Field label="Apellidos">
          <input value={form.lastName} onChange={(e) => set('lastName', e.target.value)} required maxLength={80} />
        </Field>
        <Field label="Fecha de nacimiento">
          <input type="date" value={form.birthDate} onChange={(e) => set('birthDate', e.target.value)} required />
        </Field>
        <Field label="Grado">
          <input value={form.grade} onChange={(e) => set('grade', e.target.value)} required placeholder="8vo EGB" />
        </Field>
        <Field label="Colegio">
          <input value={form.schoolId} onChange={(e) => set('schoolId', e.target.value)} required />
        </Field>
        <Field label="Representante">
          <input
            value={form.guardian.fullName}
            onChange={(e) => set('guardian', { ...form.guardian, fullName: e.target.value })}
            required
          />
        </Field>
        <Field label="Email del representante">
          <input
            type="email"
            value={form.guardian.email}
            onChange={(e) => set('guardian', { ...form.guardian, email: e.target.value })}
            required
          />
        </Field>
        <Field label="Teléfono del representante">
          <input
            value={form.guardian.phone}
            onChange={(e) => set('guardian', { ...form.guardian, phone: e.target.value })}
            required
          />
        </Field>
      </div>
      <div className="form-actions">
        <button type="submit" className="btn" disabled={createStudent.isPending}>
          {createStudent.isPending ? 'Registrando…' : 'Guardar estudiante'}
        </button>
      </div>
    </form>
  )
}

function StudentDetail({ studentId }: { studentId: string }) {
  const { notify, notifyError } = useToast()
  const student = useStudent(studentId)
  const enrollments = useEnrollments(studentId)
  const events = useStudentEvents(studentId)
  const createEnrollment = useCreateEnrollment()
  const [schoolYear, setSchoolYear] = useState('2026-2027')

  async function onEnroll(event: FormEvent) {
    event.preventDefault()
    if (!student.data) return
    try {
      await createEnrollment.mutateAsync({
        studentId,
        schoolYear,
        grade: student.data.grade,
        schoolId: student.data.schoolId,
      })
      notify('success', 'Matrícula confirmada. Se publicó el evento StudentEnrolled.')
    } catch (error) {
      notifyError(error)
    }
  }

  if (student.isPending) return <section className="card"><Loading label="Cargando ficha…" /></section>
  if (student.isError) return <section className="card"><ErrorState error={student.error} onRetry={() => student.refetch()} /></section>

  const data = student.data
  return (
    <section className="card" id="ficha">
      <div className="section-head">
        <h2>{data.firstName} {data.lastName}</h2>
        <div>
          <Badge tone="brand">{data.code}</Badge>{' '}
          <Badge tone={statusTone(data.status)}>{data.status}</Badge>{' '}
          <Badge tone={statusTone(data.financialStatus)}>{data.financialStatus}</Badge>
        </div>
      </div>

      <div className="stat-grid">
        <div className="stat"><span className="stat-value">{data.grade}</span><span className="stat-label">Grado · {data.schoolId}</span></div>
        <div className="stat"><span className="stat-value">{data.identification}</span><span className="stat-label">Cédula</span></div>
        <div className="stat"><span className="stat-value">{data.guardian.fullName}</span><span className="stat-label">{data.guardian.email}</span></div>
      </div>

      <h3 style={{ marginTop: '1.25rem' }}>Matrículas</h3>
      <form onSubmit={onEnroll} className="form-grid">
        <Field label="Año lectivo">
          <input value={schoolYear} onChange={(e) => setSchoolYear(e.target.value)} required maxLength={20} />
        </Field>
        <button type="submit" className="btn" disabled={createEnrollment.isPending}>
          {createEnrollment.isPending ? 'Matriculando…' : 'Crear matrícula'}
        </button>
      </form>

      {enrollments.isPending && <Loading />}
      {enrollments.isError && <ErrorState error={enrollments.error} />}
      {enrollments.data && enrollments.data.length === 0 && <EmptyState message="Sin matrículas registradas." />}
      {enrollments.data && enrollments.data.length > 0 && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Año lectivo</th><th>Grado</th><th>Estado</th><th>Fecha</th></tr>
            </thead>
            <tbody>
              {enrollments.data.map((enrollment) => (
                <tr key={enrollment.enrollmentId}>
                  <td>{enrollment.schoolYear}</td>
                  <td>{enrollment.grade}</td>
                  <td><Badge tone={statusTone(enrollment.status)}>{enrollment.status}</Badge></td>
                  <td>{new Date(enrollment.enrolledAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <h3 style={{ marginTop: '1.25rem' }}>Historial de eventos</h3>
      {events.isPending && <Loading />}
      {events.isError && <ErrorState error={events.error} />}
      {events.data && events.data.length === 0 && <EmptyState message="Sin eventos asociados." />}
      {events.data && events.data.length > 0 && (
        <ul className="timeline">
          {events.data.map((item) => (
            <li key={item.eventId}>
              <strong>{item.eventType}</strong>{' '}
              <time>{new Date(item.occurredAt).toLocaleString()}</time>
              <div><small>correlación: {item.correlationId}</small></div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
