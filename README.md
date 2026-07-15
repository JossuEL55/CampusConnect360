# CampusConnect 360

Ecosistema funcional de integración para una red de colegios. Conecta los sistemas de
matrícula, pagos, asistencia, notificaciones y analítica mediante APIs REST, un API Gateway,
mensajería con RabbitMQ, eventos de negocio y proyecciones CQRS, con seguridad JWT,
resiliencia, trazabilidad y observabilidad. Proyecto Integrador de Integración de Sistemas.

## 1. Problema y alcance

Una organización educativa administra varios colegios cuyos datos viven en sistemas
separados: los pagos no se reflejan a tiempo en lo académico, las notificaciones son
manuales, la asistencia no se consolida y la dirección no tiene indicadores en tiempo real.

CampusConnect 360 integra esos dominios en un ecosistema de microservicios que se comunican
por eventos: cada acción de negocio (matrícula, pago, asistencia, incidente) publica un
evento al que reaccionan los demás servicios, y un servicio de analítica proyecta todo en un
dashboard directivo. La experiencia principal ocurre desde cuatro portales web.

## 2. Arquitectura

```
                          Navegador (SPA React, 4 portales)
                                     │  http://localhost:3000
                                     ▼
                        ┌───────────────────────────┐
                        │   API Gateway (YARP)       │  :8088
                        │   JWT · CORS · correlación │
                        └───────────────────────────┘
        ┌──────────────┬──────────────┼──────────────┬──────────────┐
        ▼              ▼              ▼              ▼              ▼
  Academic:5001  Payment:5002  Attendance:5003  Notification:5004  Analytics:5005
        │              │              │              │              ▲
        └──────────────┴──────► RabbitMQ ◄──────────┘   proyección CQRS
                          exchange campus.events (topic)
                          exchange campus.dlx (dead-letter)

  PostgreSQL 6 esquemas (academic, payments, attendance, notifications, analytics, auth)
  Seq (logs Serilog)  ·  Docker Compose orquesta todo
```

| Componente | Puerto host | Responsabilidad |
|---|---|---|
| API Gateway (YARP) | 8088 | Entrada única, autenticación JWT, autorización por rol, CORS, `X-Correlation-Id` |
| AcademicService | 5001 | Estudiantes, matrículas; publica `StudentEnrolled`, `StudentStatusUpdated` |
| PaymentService | 5002 | Deudas y pagos; publica `PaymentConfirmed`; réplica local de estudiantes |
| AttendanceService | 5003 | Asistencia e incidentes; publica `AttendanceRecorded`, `IncidentReported` |
| NotificationService | 5004 | Notificaciones simuladas; retry, DLQ; publica `NotificationSent`/`NotificationFailed` |
| AnalyticsService | 5005 | Lado de lectura CQRS; proyecta todos los eventos; alimenta el dashboard |
| PostgreSQL | 5432 | Una base, seis esquemas independientes |
| RabbitMQ | 5672 / 15672 | Broker de eventos + panel de gestión |
| Seq | 8081 | Logs estructurados de todos los servicios |
| Frontend React | 3000 | 4 portales: Académico, Financiero, Docente/Bienestar, Directivo |

## 3. Requisitos

- Docker Desktop (con Docker Compose)
- .NET SDK 10 (solo para ejecución sin Docker o correr los tests)
- Node.js 20+ (solo para el frontend en modo desarrollo)

Comprobar: `git --version`, `docker --version`, `docker compose version`, `docker info`.

## 4. Levantar el ecosistema

Desde la raíz del repositorio:

```powershell
git checkout develop
Copy-Item .env.example .env      # ajustar contraseñas y JWT_SECRET (ver abajo)
.\scripts\deploy.ps1             # construye imágenes, levanta contenedores y valida health
```

Equivalente manual: `docker compose up -d --build`.

Deben quedar arriba 9 contenedores (`docker compose ps`): gateway, academic, payment,
attendance, notification, analytics, postgres, rabbitmq, seq.

El **frontend** se levanta aparte en modo desarrollo:

```powershell
cd frontend
Copy-Item .env.example .env
npm install
npm run dev                      # http://localhost:3000
```

### Variables de entorno (.env de ejemplo)

```
POSTGRES_USER=campus_admin
POSTGRES_PASSWORD=postcampus
POSTGRES_DB=campusconnect
POSTGRES_PORT=5432
RABBITMQ_DEFAULT_USER=campus
RABBITMQ_DEFAULT_PASS=campusrabbit
RABBITMQ_PORT=5672
RABBITMQ_MANAGEMENT_PORT=15672
SEQ_PORT=8081
SEQ_INGESTION_PORT=5341
SEQ_ADMIN_USERNAME=admin
SEQ_ADMIN_PASSWORD=campuseq
GATEWAY_PORT=8080
JWT_SECRET=clave-de-al-menos-32-caracteres
JWT_ISSUER=CampusConnect360
JWT_AUDIENCE=CampusConnect360Clients
AUTH_DEMO_PASSWORD=Demo2026*
```

`JWT_SECRET` debe tener al menos 32 caracteres. El archivo `.env` no se sube al repositorio.

## 5. Verificar el despliegue

| Recurso | URL | Acceso |
|---|---|---|
| Frontend (4 portales) | http://localhost:3000 | usuarios de prueba (ver §6) |
| Gateway — health agregado | http://localhost:8088/health/services | público |
| Swagger/OpenAPI por servicio | Academic http://localhost:5001/swagger · Payments http://localhost:5002/swagger · Attendance http://localhost:5003/openapi/v1.json · Notifications http://localhost:5004/openapi/v1.json · Analytics http://localhost:5005/openapi/v1.json | público (evidencia técnica) |
| RabbitMQ (panel) | http://localhost:15672 | `campus` / `campusrabbit` |
| Seq (logs) | http://localhost:8081 | `admin` / `campuseq` |

`Invoke-RestMethod http://localhost:8088/health/services` debe devolver `Healthy` y 5/5.
`.\scripts\test-gateway-routes.ps1` prueba las rutas por servicio.

## 6. Usuarios de prueba

Todos usan la contraseña de `AUTH_DEMO_PASSWORD` (por defecto `Demo2026*`).

| Usuario | Rol | Portal |
|---|---|---|
| `secretaria` | Academic | Portal Académico / Secretaría |
| `finanzas` | Finance | Portal Financiero / Pagos |
| `docente` | Teacher | Portal Docente / Bienestar |
| `direccion` | Director | Dashboard Directivo |
| `admin` | Admin | Acceso a los cuatro portales |

## 7. Recorrido de demostración — "Un día de operación"

1. **Secretaría** entra al portal Académico, registra un estudiante y crea su matrícula → se publica `StudentEnrolled`.
2. **Notificaciones** consume el evento y registra una notificación de bienvenida; **Analítica** actualiza sus indicadores.
3. **Finanzas** entra al portal Financiero, ve la deuda del estudiante y confirma el pago → se publica `PaymentConfirmed`.
4. **Académico** consume el pago y cambia el estado financiero del estudiante a `UpToDate` (publica `StudentStatusUpdated`).
5. **Docente** entra a su portal, registra una ausencia y un incidente de severidad alta → se publican `AttendanceRecorded` e `IncidentReported`; Notificaciones genera alertas.
6. **Dirección** abre el Dashboard: los indicadores se refrescan cada 10 s, la bitácora lista los eventos y la traza por `correlationId` reconstruye el flujo completo entre servicios.
7. **Escenario de falla** (ver §10): se activa la falla simulada de notificaciones, se observan los reintentos, el mensaje cae a la DLQ y el dashboard refleja los fallos; luego se reprocesa.

## 8. Eventos de negocio

Siete eventos, todos con el sobre estándar del Shared Kernel (`eventId`, `eventType`,
`version`, `occurredAt`, `correlationId`, `source`, `entityId`, `data`). El `eventId` es la
clave de idempotencia.

| Evento | Publica | Consumen | Routing key |
|---|---|---|---|
| StudentEnrolled | Academic | Payments, Attendance, Notifications, Analytics | `academic.student.enrolled` |
| PaymentConfirmed | Payments | Academic, Notifications, Analytics | `payments.payment.confirmed` |
| AttendanceRecorded | Attendance | Notifications (Absent/Late), Analytics | `attendance.record.registered` |
| IncidentReported | Attendance | Notifications, Analytics | `attendance.incident.reported` |
| NotificationSent | Notifications | Analytics | `notifications.notification.sent` |
| NotificationFailed | Notifications | Analytics | `notifications.notification.failed` |
| StudentStatusUpdated | Academic | Analytics | `academic.student.status-updated` |

Para poblar el dashboard con datos de demostración sin usar los portales:
`.\scripts\publish-demo-events.ps1`.

## 9. Patrones de integración evidenciados

| Patrón | Dónde |
|---|---|
| API Gateway | YARP como entrada única con JWT y autorización por rol |
| Publish/Subscribe | `StudentEnrolled` lo consumen cuatro servicios con colas propias |
| Point-to-Point | cada cola la procesa un único consumidor |
| Message Channel | exchange `campus.events` (topic) y colas dedicadas por consumidor |
| Event Message | sobre estándar de eventos del Shared Kernel |
| Idempotent Receiver | tabla `processed_events` verificada antes de aplicar cada evento |
| Dead Letter Channel | exchange `campus.dlx` → cola `notifications.dlq` |
| Outbox | evento guardado en `outbox_messages` en la misma transacción del comando |
| CQRS / vista analítica | AnalyticsService proyecta eventos a tablas de lectura del dashboard |
| Health Check API | `/api/<servicio>/health` y estado agregado en el Gateway |
| Trazabilidad | `X-Correlation-Id` propagado y `event_log` + endpoint de traza |

## 10. Resiliencia — escenario de falla

Reintentos con backoff (3 intentos), Dead Letter Queue e idempotencia:

```powershell
# 1. Activar la falla simulada de notificaciones
Invoke-RestMethod -Method Post -Uri http://localhost:8088/api/notifications/demo/failure `
  -Headers @{ Authorization = "Bearer $token" } -ContentType application/json -Body '{"enabled":true}'
# 2. Generar un evento que dispare notificación (registrar una ausencia)
# 3. Observar en Seq los 3 reintentos; el mensaje cae a notifications.dlq
# 4. El dashboard muestra failedMessages y dlqDepth; el semáforo pasa a Degraded
# 5. Reprocesar: POST /api/notifications/{id}/retry
```

También son escenarios válidos: detener un servicio (`docker stop campusconnect-notification`)
y ver el health agregado en `Degraded`, o publicar un evento con formato inválido y verlo
descartado con registro de error.

## 11. Observabilidad

- **Serilog → Seq** (http://localhost:8081): logs estructurados con `ServiceName`,
  `CorrelationId`, `RequestPath`, `StatusCode`, `Elapsed`.
- **Health checks**: por servicio y agregado en el Gateway (`/health/services`).
- **Trazabilidad**: `X-Correlation-Id` viaja de la petición al evento y queda en `event_log`;
  el endpoint `/api/analytics/events/{correlationId}/trace` reconstruye el flujo.

## 12. Integración de datos (CQRS)

Los servicios de dominio son el lado de escritura; el AnalyticsService es el lado de lectura
y se alimenta **solo** de eventos (nunca consulta las bases de los demás). Proyecta a
`dashboard_summary`, `student_view`, `event_log`, `daily_metrics` y `failed_messages`, que
alimentan los endpoints del dashboard directivo. Consistencia eventual comunicada en la UI
con la marca `generatedAt`.

## 13. Estructura del repositorio

```
├── docker-compose.yml            # orquestación de todo el ecosistema
├── .env.example                  # variables de entorno de ejemplo
├── docs/                         # colección Postman (respaldo técnico)
├── scripts/                      # deploy, stop, reset, pruebas de rutas, eventos demo
├── infrastructure/
│   ├── postgres/                 # init de esquemas
│   └── rabbitmq/                 # topología declarativa (exchanges, colas, bindings, usuario)
├── backend/
│   ├── Gateway/                  # YARP + identidad JWT
│   ├── SharedKernel/             # sobre de eventos, contratos, configuración común
│   ├── Services/                 # Academic, Payment, Attendance, Notification, Analytics
│   └── Tests/                    # pruebas unitarias por servicio (xUnit)
└── frontend/                     # SPA React (4 portales), ver frontend/README.md
```

## 14. Pruebas

```powershell
dotnet test CampusConnect360.slnx     # backend (xUnit)
cd frontend; npm run build            # verificación de tipos del frontend
```

## 15. Flujo de trabajo Git

`feature/*` → `develop` → `main`. Cada integrante trabaja en su rama y abre Pull Request a
`develop`. El paso a `main` se hace cuando la solución compila, Docker levanta el ecosistema,
el health agregado está `Healthy` y se han verificado rutas y autenticación.

## 16. Responsabilidades del equipo

| Integrante | Responsabilidad |
|---|---|
| Josué Ayala | Infraestructura: Gateway YARP, RabbitMQ, PostgreSQL, Seq, Docker Compose, Shared Kernel |
| Priscila Zúñiga | AcademicService |
| Juliana Sosa | PaymentService |
| Diego Vega | AttendanceService y NotificationService |
| Daniela Mora | Frontend (4 portales) y AnalyticsService (CQRS) |

## 17. Declaración de uso de IA

Se utilizaron herramientas de IA generativa como apoyo para generar código base, proponer
estilos visuales del frontend, redactar documentación y crear pruebas. El equipo comprende,
adapta, integra y defiende todo el código presentado; las decisiones arquitectónicas y la
integración entre servicios son propias.

## 18. Limitaciones y mejoras futuras

- El envío de correo/SMS es **simulado** (se persiste la notificación y se registra en logs).
- La consistencia del dashboard es eventual (segundos de retraso), aceptable para indicadores
  directivos.
- Mejoras futuras: paginación server-side en más listados, métricas por colegio, alertas
  proactivas y despliegue en la nube con imágenes publicadas en un registry.
