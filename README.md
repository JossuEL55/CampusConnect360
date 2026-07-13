# CampusConnect360

Ecosistema de integración para una red de colegios, desarrollado con .NET 10, YARP, RabbitMQ, PostgreSQL, Serilog, Seq y Docker Compose.

## Objetivo

CampusConnect360 integra los procesos académicos, financieros, de asistencia, notificaciones y analítica mediante:

- API Gateway con YARP.
- Microservicios independientes.
- Mensajería con RabbitMQ.
- Persistencia separada por esquemas en PostgreSQL.
- Autenticación JWT y autorización por roles.
- Trazabilidad con `X-Correlation-Id`.
- Observabilidad con Serilog y Seq.
- Health checks individuales y agregados.
- Contenedores Docker y despliegue con Docker Compose.

## Arquitectura

```text
Frontend React
      |
      v
API Gateway - YARP
      |
      +--> AcademicService
      +--> PaymentService
      +--> AttendanceService
      +--> NotificationService
      +--> AnalyticsService

Microservicios <--> RabbitMQ
Microservicios <--> PostgreSQL
Microservicios ---> Seq
```

## Puertos

| Componente | Puerto en la máquina | Puerto interno |
|---|---:|---:|
| Gateway | 8088 | 8080 |
| AcademicService | 5001 | 8080 |
| PaymentService | 5002 | 8080 |
| AttendanceService | 5003 | 8080 |
| NotificationService | 5004 | 8080 |
| AnalyticsService | 5005 | 8080 |
| PostgreSQL | 5432 | 5432 |
| RabbitMQ AMQP | 5672 | 5672 |
| RabbitMQ Management | 15672 | 15672 |
| Seq | 8081 | 80 |
| Seq ingestion | 5341 | 5341 |

> El Gateway se expone por defecto en `http://localhost:8088`. Internamente, el contenedor escucha en el puerto `8080`.

## Requisitos

Instalar antes de comenzar:

- Git
- Docker Desktop
- Docker Compose
- PowerShell
- .NET SDK 10
- Node.js y npm para el frontend

Comprobación rápida:

```powershell
git --version
dotnet --info
docker --version
docker compose version
node --version
npm --version
```

## Instalación

Clonar el repositorio:

```powershell
git clone https://github.com/JossuEL55/CampusConnect360.git
cd CampusConnect360
```

Cambiar a la rama de integración:

```powershell
git checkout develop
git pull origin develop
```

Crear el archivo local de variables de entorno:

```powershell
Copy-Item .env.example .env
```

Editar `.env` y reemplazar los valores de ejemplo.

Configuración mínima:

```env
GATEWAY_PORT=8088

POSTGRES_USER=campus_admin
POSTGRES_PASSWORD=CHANGE_ME
POSTGRES_DB=campusconnect
POSTGRES_PORT=5432

RABBITMQ_DEFAULT_USER=campus
RABBITMQ_DEFAULT_PASS=CHANGE_ME
RABBITMQ_PORT=5672
RABBITMQ_MANAGEMENT_PORT=15672

SEQ_PORT=8081
SEQ_INGESTION_PORT=5341
SEQ_ADMIN_USERNAME=admin
SEQ_ADMIN_PASSWORD=CHANGE_ME

JWT_SECRET=CHANGE_WITH_A_RANDOM_SECRET_AT_LEAST_32_CHARACTERS
AUTH_DEMO_PASSWORD=CHANGE_ME
```

El archivo `.env` no debe subirse al repositorio.

Verificar:

```powershell
git check-ignore -v .env
```

## Despliegue recomendado

Desde la raíz del repositorio:

```powershell
.\scripts\deploy.ps1
```

Este script:

1. valida que exista `.env`;
2. comprueba que Docker esté activo;
3. valida `docker-compose.yml`;
4. construye las imágenes;
5. levanta los contenedores;
6. muestra su estado;
7. verifica el estado agregado del ecosistema.

## Despliegue manual

```powershell
docker compose config --quiet
docker compose up -d --build
docker compose ps
```

## Verificación del ecosistema

Health agregado:

```powershell
Invoke-RestMethod `
  -Uri http://localhost:8088/health/services |
ConvertTo-Json -Depth 5
```

Resultado esperado:

```text
status          : Healthy
healthyServices : 5
totalServices   : 5
```

Validar las cinco rutas del Gateway:

```powershell
.\scripts\test-gateway-routes.ps1
```

## Accesos

- Gateway: `http://localhost:8088`
- RabbitMQ Management: `http://localhost:15672`
- Seq: `http://localhost:8081`

## Usuarios de prueba

| Usuario | Rol |
|---|---|
| secretaria | Academic |
| finanzas | Finance |
| docente | Teacher |
| direccion | Director |
| admin | Admin |

La contraseña común se obtiene de:

```env
AUTH_DEMO_PASSWORD
```

## Login

```powershell
$body = @{
  username = "secretaria"
  password = "VALOR_DE_AUTH_DEMO_PASSWORD"
} | ConvertTo-Json

$login = Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:8088/api/auth/login `
  -ContentType "application/json" `
  -Body $body
```

Obtener token:

```powershell
$token = $login.accessToken
```

Consultar identidad:

```powershell
Invoke-RestMethod `
  -Uri http://localhost:8088/api/auth/me `
  -Headers @{
    Authorization = "Bearer $token"
  }
```

## Trazabilidad

Toda solicitud utiliza:

```text
X-Correlation-Id
```

Si el cliente no lo envía, el Gateway genera uno automáticamente.

Ejemplo:

```powershell
Invoke-RestMethod `
  -Uri http://localhost:8088/api/academic/health `
  -Headers @{
    "X-Correlation-Id" = "demo-campus-001"
  }
```

El mismo valor puede buscarse en Seq:

```text
CorrelationId = 'demo-campus-001'
```

## Health checks

Individuales:

```text
/api/academic/health
/api/payments/health
/api/attendance/health
/api/notifications/health
/api/analytics/health
```

Agregado:

```text
/health/services
```

Estados posibles:

- `Healthy`
- `Degraded`
- `Down`

## RabbitMQ

Topología inicial:

### Exchanges

```text
campus.events
campus.dlx
```

### Colas

```text
payments.student-enrolled
attendance.student-enrolled
academic.payment-confirmed
notifications.inbox
analytics.all-events
notifications.dlq
```

## PostgreSQL

Esquemas iniciales:

```text
academic
payments
attendance
notifications
analytics
auth
```

Comprobación:

```powershell
docker exec -it campusconnect-postgres `
  psql -U campus_admin -d campusconnect -c "\dn"
```

## Scripts disponibles

| Script | Función |
|---|---|
| `scripts/deploy.ps1` | Construye y levanta el ecosistema |
| `scripts/stop-environment.ps1` | Detiene contenedores sin borrar datos |
| `scripts/reset-environment.ps1` | Elimina contenedores y volúmenes |
| `scripts/start-backend.ps1` | Levanta el backend local con .NET |
| `scripts/test-gateway-routes.ps1` | Valida rutas YARP |

## Detener el entorno

```powershell
.\scripts\stop-environment.ps1
```

O:

```powershell
docker compose down
```

## Reinicio limpio

> Este proceso elimina los datos locales de PostgreSQL, RabbitMQ y Seq.

```powershell
.\scripts\reset-environment.ps1
```

Después:

```powershell
.\scripts\deploy.ps1
```

## Ejecución local sin Docker

Compilar:

```powershell
dotnet build CampusConnect360.slnx
```

Configurar secretos locales:

```powershell
dotnet user-secrets set "Jwt:Secret" "SECRET" `
  --project backend/Gateway/Gateway.csproj

dotnet user-secrets set "Auth:DemoPassword" "PASSWORD" `
  --project backend/Gateway/Gateway.csproj
```

Levantar backend:

```powershell
.\scripts\start-backend.ps1
```

## Estrategia de ramas

```text
feature/* -> develop -> main
```

- `feature/*`: trabajo individual.
- `develop`: integración grupal.
- `main`: versión estable.

No se debe trabajar directamente sobre `main`.

## Solución de problemas

### El puerto 8088 está ocupado

Cambiar en `.env`:

```env
GATEWAY_PORT=8090
```

Luego:

```powershell
docker compose down
docker compose up -d --build
```

En ese caso, ajustar temporalmente las URLs utilizadas por frontend y scripts.

### Docker no responde

```powershell
docker info
```

### Un contenedor no inicia

```powershell
docker compose ps -a
docker compose logs <servicio> --tail 100
```

### Gateway no enruta

Verificar que dentro de Docker use:

```text
http://academic-service:8080
http://payment-service:8080
http://attendance-service:8080
http://notification-service:8080
http://analytics-service:8080
```

## Documentación completa

Consultar:

```text
docs/MANUAL_INFRAESTRUCTURA.md
```