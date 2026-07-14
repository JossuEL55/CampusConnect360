import { useState } from 'react'
import type { FormEvent } from 'react'
import { Badge, EmptyState, ErrorState, Field, Loading, Stat, statusTone } from '../../shared/ui/bits'
import { useDashboard, useEcosystemStatus, useEventLog, useFailures, useNotifications, useTrace } from './api'
import type { EventFilters } from './api'

const money = new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' })

type Tab = 'eventos' | 'traza' | 'fallos' | 'notificaciones' | 'ecosistema'

export function DirectorDashboard() {
  const dashboard = useDashboard()
  const [tab, setTab] = useState<Tab>('eventos')

  return (
    <>
      <div className="section-head">
        <h1>Dashboard Directivo</h1>
        {dashboard.data && (
          <div>
            <Badge tone={statusTone(dashboard.data.ecosystemStatus)}>
              Ecosistema {dashboard.data.ecosystemStatus}
            </Badge>{' '}
            <small>Actualizado {new Date(dashboard.data.generatedAt).toLocaleTimeString()} · se refresca cada 10 s</small>
          </div>
        )}
      </div>

      {dashboard.isPending && <Loading label="Cargando indicadores…" />}
      {dashboard.isError && <ErrorState error={dashboard.error} onRetry={() => dashboard.refetch()} />}

      {dashboard.data && (
        <>
          <div className="stat-grid">
            <Stat label="Estudiantes matriculados" value={dashboard.data.students.enrolledTotal} hint={`Hoy: ${dashboard.data.students.enrolledToday}`} />
            <Stat label="Pagos confirmados" value={dashboard.data.payments.confirmedTotal} hint={money.format(dashboard.data.payments.confirmedAmount)} />
            <Stat label="Registros de asistencia" value={dashboard.data.attendance.recordsTotal} hint={`Ausencias hoy: ${dashboard.data.attendance.absencesToday}`} />
            <Stat label="Incidentes reportados" value={dashboard.data.incidents.reportedTotal} hint={`Severidad alta: ${dashboard.data.incidents.highSeverity}`} />
            <Stat label="Eventos procesados" value={dashboard.data.events.processedTotal} />
            <Stat label="Mensajes fallidos" value={dashboard.data.failures.failedMessages} hint={`DLQ: ${dashboard.data.failures.dlqDepth}`} />
          </div>

          <section className="card">
            <h2>Eventos por tipo</h2>
            <div>
              {Object.entries(dashboard.data.events.byType).map(([type, count]) => (
                <span key={type} style={{ marginRight: '0.5rem', display: 'inline-block', marginBottom: '0.4rem' }}>
                  <Badge tone="brand">{type}: {count}</Badge>
                </span>
              ))}
            </div>
          </section>
        </>
      )}

      <section className="card">
        <div className="tabs" role="tablist">
          {(
            [
              ['eventos', 'Bitácora de eventos'],
              ['traza', 'Traza por correlación'],
              ['fallos', 'Fallos'],
              ['notificaciones', 'Notificaciones'],
              ['ecosistema', 'Estado del ecosistema'],
            ] as [Tab, string][]
          ).map(([id, label]) => (
            <button key={id} type="button" role="tab" aria-selected={tab === id} onClick={() => setTab(id)}>
              {label}
            </button>
          ))}
        </div>
        {tab === 'eventos' && <EventLogTab />}
        {tab === 'traza' && <TraceTab />}
        {tab === 'fallos' && <FailuresTab />}
        {tab === 'notificaciones' && <NotificationsTab />}
        {tab === 'ecosistema' && <EcosystemTab />}
      </section>
    </>
  )
}

const EVENT_TYPES = [
  'StudentEnrolled',
  'PaymentConfirmed',
  'AttendanceRecorded',
  'IncidentReported',
  'NotificationSent',
  'NotificationFailed',
  'StudentStatusUpdated',
]

function EventLogTab() {
  const [filters, setFilters] = useState<EventFilters>({})
  const [page, setPage] = useState(1)
  const events = useEventLog(filters, page)
  const totalPages = events.data ? Math.max(1, Math.ceil(events.data.totalCount / events.data.pageSize)) : 1

  return (
    <>
      <div className="form-grid">
        <Field label="Tipo de evento">
          <select
            value={filters.type ?? ''}
            onChange={(event) => {
              setPage(1)
              setFilters((current) => ({ ...current, type: event.target.value || undefined }))
            }}
          >
            <option value="">Todos</option>
            {EVENT_TYPES.map((type) => (
              <option key={type} value={type}>{type}</option>
            ))}
          </select>
        </Field>
        <Field label="Correlación">
          <input
            value={filters.correlationId ?? ''}
            onChange={(event) => {
              setPage(1)
              setFilters((current) => ({ ...current, correlationId: event.target.value || undefined }))
            }}
            placeholder="corr-…"
          />
        </Field>
      </div>

      {events.isPending && <Loading />}
      {events.isError && <ErrorState error={events.error} onRetry={() => events.refetch()} />}
      {events.data && events.data.items.length === 0 && <EmptyState message="Sin eventos para los filtros elegidos." />}
      {events.data && events.data.items.length > 0 && (
        <>
          <div className="table-wrap">
            <table>
              <thead>
                <tr><th>Tipo</th><th>Origen</th><th>Ocurrido</th><th>Correlación</th></tr>
              </thead>
              <tbody>
                {events.data.items.map((entry) => (
                  <tr key={entry.eventId}>
                    <td><Badge tone="brand">{entry.eventType}</Badge></td>
                    <td>{entry.source}</td>
                    <td>{new Date(entry.occurredAt).toLocaleString()}</td>
                    <td><small>{entry.correlationId}</small></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="pager">
            <button type="button" className="btn btn-ghost" disabled={page <= 1} onClick={() => setPage(page - 1)}>Anterior</button>
            <span>Página {page} de {totalPages} · {events.data.totalCount} eventos</span>
            <button type="button" className="btn btn-ghost" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Siguiente</button>
          </div>
        </>
      )}
    </>
  )
}

function TraceTab() {
  const [input, setInput] = useState('')
  const [correlationId, setCorrelationId] = useState<string | null>(null)
  const trace = useTrace(correlationId)

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    setCorrelationId(input.trim() || null)
  }

  return (
    <>
      <form onSubmit={onSubmit} className="form-grid">
        <Field label="Identificador de correlación">
          <input value={input} onChange={(event) => setInput(event.target.value)} placeholder="corr-demo-…" required />
        </Field>
        <button type="submit" className="btn">Trazar flujo</button>
      </form>

      {trace.isFetching && <Loading label="Buscando la traza…" />}
      {trace.isError && <ErrorState error={trace.error} />}
      {trace.data && (
        <>
          <p><strong>{trace.data.totalEvents}</strong> eventos en el flujo <code>{trace.data.correlationId}</code>:</p>
          <ul className="timeline">
            {trace.data.steps.map((step, index) => (
              <li key={step.eventId}>
                <strong>{index + 1}. {step.eventType}</strong> · {step.source}{' '}
                <time>{new Date(step.occurredAt).toLocaleString()}</time>
              </li>
            ))}
          </ul>
        </>
      )}
    </>
  )
}

function FailuresTab() {
  const [page, setPage] = useState(1)
  const failures = useFailures(page)

  return (
    <>
      {failures.isPending && <Loading />}
      {failures.isError && <ErrorState error={failures.error} onRetry={() => failures.refetch()} />}
      {failures.data && (
        <>
          <p>Profundidad actual de la DLQ: <Badge tone={failures.data.dlqDepth > 0 ? 'err' : 'ok'}>{failures.data.dlqDepth}</Badge></p>
          {failures.data.items.length === 0 && <EmptyState message="Sin notificaciones fallidas registradas." />}
          {failures.data.items.length > 0 && (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr><th>Evento origen</th><th>Canal</th><th>Destinatario</th><th>Intentos</th><th>Razón</th><th>Fecha</th></tr>
                </thead>
                <tbody>
                  {failures.data.items.map((failure) => (
                    <tr key={failure.eventId}>
                      <td>{failure.sourceEventType ?? '—'}</td>
                      <td>{failure.channel ?? '—'}</td>
                      <td>{failure.recipient ?? '—'}</td>
                      <td>{failure.attempts ?? '—'}</td>
                      <td>{failure.failureReason ?? '—'}</td>
                      <td>{new Date(failure.occurredAt).toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <div className="pager">
            <button type="button" className="btn btn-ghost" disabled={page <= 1} onClick={() => setPage(page - 1)}>Anterior</button>
            <span>Página {page} · {failures.data.totalCount} fallos</span>
            <button
              type="button"
              className="btn btn-ghost"
              disabled={page * failures.data.pageSize >= failures.data.totalCount}
              onClick={() => setPage(page + 1)}
            >
              Siguiente
            </button>
          </div>
        </>
      )}
    </>
  )
}

function EcosystemTab() {
  const ecosystem = useEcosystemStatus()

  return (
    <>
      {ecosystem.isPending && <Loading />}
      {ecosystem.isError && <ErrorState error={ecosystem.error} onRetry={() => ecosystem.refetch()} />}
      {ecosystem.data && (
        <>
          <p>
            Estado general: <Badge tone={statusTone(ecosystem.data.ecosystemStatus)}>{ecosystem.data.ecosystemStatus}</Badge>{' '}
            · Broker: <Badge tone={ecosystem.data.broker === 'Up' ? 'ok' : 'err'}>{ecosystem.data.broker}</Badge>{' '}
            · DLQ: <Badge tone={ecosystem.data.dlqDepth > 0 ? 'warn' : 'ok'}>{ecosystem.data.dlqDepth}</Badge>
          </p>
          <div className="table-wrap">
            <table>
              <thead>
                <tr><th>Servicio</th><th>Estado</th></tr>
              </thead>
              <tbody>
                {ecosystem.data.services.map((service) => (
                  <tr key={service.name}>
                    <td>{service.name}</td>
                    <td><Badge tone={statusTone(service.status)}>{service.status}</Badge></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </>
  )
}

function NotificationsTab() {
  const [status, setStatus] = useState('')
  const notifications = useNotifications(status)

  return (
    <>
      <div className="form-grid">
        <Field label="Estado">
          <select value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="">Todas</option>
            <option value="Sent">Enviadas</option>
            <option value="Failed">Fallidas</option>
            <option value="Pending">Pendientes</option>
          </select>
        </Field>
      </div>
      {notifications.isPending && <Loading />}
      {notifications.isError && <ErrorState error={notifications.error} onRetry={() => notifications.refetch()} />}
      {notifications.data && notifications.data.length === 0 && (
        <EmptyState message="Sin notificaciones registradas." />
      )}
      {notifications.data && notifications.data.length > 0 && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Evento origen</th><th>Canal</th><th>Destinatario</th><th>Estado</th><th>Intentos</th><th>Fecha</th></tr>
            </thead>
            <tbody>
              {notifications.data.map((notification) => (
                <tr key={notification.notificationId}>
                  <td>{notification.sourceEventType ?? '—'}</td>
                  <td>{notification.channel ?? '—'}</td>
                  <td>{notification.recipient ?? '—'}</td>
                  <td><Badge tone={statusTone(notification.status ?? '')}>{notification.status ?? '—'}</Badge></td>
                  <td>{notification.attempts ?? '—'}</td>
                  <td>{notification.sentAt ? new Date(notification.sentAt).toLocaleString() : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
