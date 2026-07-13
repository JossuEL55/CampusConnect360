# Manual de infraestructura de CampusConnect360

## 1. Propósito

Este manual explica cómo preparar, levantar, verificar, detener y reconstruir el entorno local de CampusConnect360. Está dirigido a integrantes del equipo que necesiten ejecutar el ecosistema desde una instalación nueva.

## 2. Componentes del ecosistema

El entorno incluye:

- API Gateway con YARP, JWT, autorización por roles y CORS.
- Microservicios Academic, Payment, Attendance, Notification y Analytics.
- SharedKernel para contratos y componentes compartidos.
- PostgreSQL 17 con seis esquemas.
- RabbitMQ 4 con topología declarativa.
- Seq para centralización de logs.
- Health checks individuales y estado agregado.
- Dockerfiles, Docker Compose y scripts operativos.

## 3. Estructura principal

```text
CampusConnect360/
|-- backend/
|   |-- Gateway/
|   |-- Services/
|   |   |-- AcademicService/
|   |   |-- PaymentService/
|   |   |-- AttendanceService/
|   |   |-- NotificationService/
|   |   `-- AnalyticsService/
|   `-- SharedKernel/
|-- infrastructure/
|   |-- postgres/
|   |-- rabbitmq/
|   `-- seq/
|-- scripts/
|-- docs/
|-- docker-compose.yml
|-- .env.example
|-- .dockerignore
`-- CampusConnect360.slnx
```

## 4. Requisitos

Para ejecutar todo con Docker:

- Windows 10 u 11.
- Git.
- Docker Desktop con Docker Compose.
- PowerShell.

Para ejecutar el backend sin Docker también se necesita .NET SDK 10. Node.js y npm serán necesarios para trabajar con el frontend.

Comprobar las herramientas instaladas:

```powershell
git --version
docker --version
docker compose version
docker info
```

`docker info` debe finalizar correctamente. Si falla, abrir Docker Desktop y esperar hasta que el motor esté disponible.

## 5. Obtener el proyecto

```powershell
git clone https://github.com/JossuEL55/CampusConnect360.git
cd CampusConnect360
git checkout develop
git pull origin develop
```

Todos los comandos de este manual deben ejecutarse desde la raíz del repositorio, salvo que se indique lo contrario.

## 6. Preparar las variables de entorno

Crear el archivo local a partir de la plantilla:

```powershell
Copy-Item .env.example .env
```

Abrir `.env` y reemplazar los valores de ejemplo. Como mínimo deben estar configuradas estas variables:

```env
POSTGRES_USER=campus_admin
POSTGRES_PASSWORD=UNA_CONTRASENA_LOCAL
POSTGRES_DB=campusconnect
POSTGRES_PORT=5432

RABBITMQ_DEFAULT_USER=campus
RABBITMQ_DEFAULT_PASS=UNA_CONTRASENA_LOCAL
RABBITMQ_PORT=5672
RABBITMQ_MANAGEMENT_PORT=15672

SEQ_PORT=8081
SEQ_INGESTION_PORT=5341
SEQ_ADMIN_USERNAME=admin
SEQ_ADMIN_PASSWORD=UNA_CONTRASENA_LOCAL

JWT_SECRET=UNA_CLAVE_ALEATORIA_DE_AL_MENOS_32_CARACTERES
AUTH_DEMO_PASSWORD=UNA_CONTRASENA_DE_DEMOSTRACION
```

No se deben guardar secretos reales en `.env.example` ni subir `.env` al repositorio. Verificar que Git ignore el archivo:

```powershell
git check-ignore -v .env
```

> Nota: actualmente el Gateway se publica en el puerto fijo `8088` dentro de `docker-compose.yml`. Una variable `GATEWAY_PORT` en `.env` no cambia ese puerto mientras Compose conserve el mapeo `8088:8080`.

## 7. Puertos

| Componente | Puerto del host | Puerto del contenedor |
|---|---:|---:|
| Gateway | 8088 | 8080 |
| AcademicService | 5001 | 8080 |
| PaymentService | 5002 | 8080 |
| AttendanceService | 5003 | 8080 |
| NotificationService | 5004 | 8080 |
| AnalyticsService | 5005 | 8080 |
| PostgreSQL | 5432 | 5432 |
| RabbitMQ | 5672 | 5672 |
| RabbitMQ Management | 15672 | 15672 |
| Seq | 8081 | 80 |
| Seq ingestion | 5341 | 5341 |

Dentro de la red de Docker, los contenedores se comunican por nombre y puerto interno. Por ejemplo, el Gateway usa `http://academic-service:8080`, no `localhost:5001`.

## 8. Levantar el entorno automáticamente

Ejecutar:

```powershell
.\scripts\deploy.ps1
```

El script:

1. Comprueba que exista `.env`.
2. Comprueba que Docker esté disponible.
3. valida `docker-compose.yml`.
4. Construye las imágenes.
5. Crea e inicia los contenedores.
6. Muestra su estado.
7. Consulta el health check agregado.

La primera ejecución puede tardar varios minutos porque Docker debe descargar imágenes y restaurar dependencias.

## 9. Levantar el entorno manualmente

Como alternativa:

```powershell
docker compose config --quiet
docker compose up -d --build
docker compose ps
```

Contenedores esperados:

```text
campusconnect-gateway
campusconnect-academic
campusconnect-payment
campusconnect-attendance
campusconnect-notification
campusconnect-analytics
campusconnect-postgres
campusconnect-rabbitmq
campusconnect-seq
```

## 10. Verificar el despliegue

Comprobar el Gateway:

```powershell
Invoke-RestMethod -Uri http://localhost:8088/health
```

Comprobar los cinco microservicios mediante el estado agregado:

```powershell
Invoke-RestMethod `
  -Uri http://localhost:8088/health/services |
ConvertTo-Json -Depth 5
```

Con todos los servicios disponibles, el resultado debe indicar `Healthy` y `5` de `5` servicios saludables.

Probar las rutas publicadas por YARP:

```powershell
.\scripts\test-gateway-routes.ps1
```

También se pueden consultar individualmente:

```text
http://localhost:8088/api/academic/health
http://localhost:8088/api/payments/health
http://localhost:8088/api/attendance/health
http://localhost:8088/api/notifications/health
http://localhost:8088/api/analytics/health
```

## 11. Autenticación y autorización

Usuarios de demostración:

| Usuario | Rol |
|---|---|
| secretaria | Academic |
| finanzas | Finance |
| docente | Teacher |
| direccion | Director |
| admin | Admin |

Todos utilizan el valor configurado en `AUTH_DEMO_PASSWORD`.

Ejemplo de inicio de sesión:

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

$token = $login.accessToken
```

Consultar el usuario autenticado:

```powershell
Invoke-RestMethod `
  -Uri http://localhost:8088/api/auth/me `
  -Headers @{ Authorization = "Bearer $token" }
```

Políticas del Gateway:

- `AcademicPolicy` para AcademicService.
- `FinancePolicy` para PaymentService.
- `TeacherPolicy` para AttendanceService.
- `AuthenticatedPolicy` para NotificationService.
- `DirectorPolicy` para AnalyticsService.
- El rol `Admin` puede acceder a todos los módulos.

## 12. Observabilidad

Seq está disponible en:

```text
http://localhost:8081
```

Propiedades útiles para buscar eventos:

```text
ServiceName
CorrelationId
RequestPath
RequestMethod
StatusCode
Elapsed
```

Ejemplos de filtro:

```text
ServiceName = 'Gateway'
CorrelationId = 'demo-campus-001'
```

Probar un identificador de correlación:

```powershell
Invoke-RestMethod `
  -Uri http://localhost:8088/api/academic/health `
  -Headers @{ "X-Correlation-Id" = "demo-campus-001" }
```

## 13. PostgreSQL y RabbitMQ

Esquemas de PostgreSQL:

```text
academic, payments, attendance, notifications, analytics, auth
```

Si se mantienen los valores sugeridos en `.env`, se pueden listar con:

```powershell
docker exec -it campusconnect-postgres `
  psql -U campus_admin -d campusconnect -c "\dn"
```

Si se cambian `POSTGRES_USER` o `POSTGRES_DB`, también se deben cambiar en ese comando.

RabbitMQ Management está disponible en:

```text
http://localhost:15672
```

Se inicia sesión con `RABBITMQ_DEFAULT_USER` y `RABBITMQ_DEFAULT_PASS`.

Exchanges principales:

```text
campus.events
campus.dlx
```

## 14. Detener o reconstruir el entorno

Detener los contenedores conservando datos:

```powershell
.\scripts\stop-environment.ps1
```

Eliminar contenedores y volúmenes:

```powershell
.\scripts\reset-environment.ps1
```

El script de reinicio solicita escribir `RESET`. Esta operación elimina los datos locales de PostgreSQL, RabbitMQ y Seq. Para reconstruir después:

```powershell
.\scripts\deploy.ps1
```

## 15. Ejecución local sin Docker

Esta modalidad requiere .NET SDK 10:

```powershell
dotnet build CampusConnect360.slnx
.\scripts\start-backend.ps1
```

Para el Gateway local, configurar secretos de usuario:

```powershell
dotnet user-secrets set "Jwt:Secret" "CLAVE_DE_AL_MENOS_32_CARACTERES" `
  --project backend/Gateway/Gateway.csproj

dotnet user-secrets set "Auth:DemoPassword" "CONTRASENA_LOCAL" `
  --project backend/Gateway/Gateway.csproj
```

No se debe iniciar simultáneamente el Gateway local y el Gateway de Docker, porque ambos intentarán utilizar el puerto `8088`.

## 16. Prueba de una falla controlada

Detener NotificationService:

```powershell
docker stop campusconnect-notification
```

Consultar nuevamente `/health/services`. El estado general debe ser `Degraded` y NotificationService debe aparecer como `Unreachable`.

Restaurar el servicio:

```powershell
docker start campusconnect-notification
```

## 17. Solución de problemas

### Docker no está disponible

```powershell
docker info
```

Abrir o reiniciar Docker Desktop si el comando falla.

### Un contenedor se reinicia o se detiene

```powershell
docker compose ps -a
docker compose logs NOMBRE_DEL_SERVICIO --tail 100
```

Ejemplo:

```powershell
docker compose logs gateway --tail 100
```

### Puerto ocupado

```powershell
Get-NetTCPConnection -LocalPort 8088 -State Listen -ErrorAction SilentlyContinue |
Select-Object LocalAddress, LocalPort, OwningProcess,
@{Name="ProcessName";Expression={(Get-Process -Id $_.OwningProcess).ProcessName}}
```

Detener la aplicación que usa el puerto. Si Docker ya está ejecutando el Gateway, no iniciar otra instancia desde Visual Studio o `start-backend.ps1`.

### El Gateway aún no responde después del despliegue

```powershell
docker compose ps
docker compose logs gateway --tail 100
```

La primera construcción puede tardar. Esperar unos segundos y repetir la consulta de health.

### RabbitMQ rechaza el inicio de sesión

```powershell
docker exec campusconnect-rabbitmq rabbitmqctl list_users
```

Comparar los usuarios con las variables configuradas en `.env`. Si se cambiaron credenciales después de crear el volumen, puede ser necesario ejecutar el reinicio limpio, teniendo presente que elimina todos los datos locales.

### Seq no recibe logs

Verificar que los servicios tengan dentro de Docker:

```text
Seq__ServerUrl=http://seq:5341
```

### Recuperación rápida de archivos versionados eliminados

Primero comprobar el estado:

```powershell
git status
```

Para recuperar un archivo eliminado desde el último commit:

```powershell
git restore ruta/del/archivo
```

No ejecutar restauraciones generales si existen cambios locales que deban conservarse.

## 18. Flujo Git del equipo

```text
feature/* -> develop -> main
```

Flujo recomendado:

```powershell
git checkout develop
git pull origin develop
git checkout -b feature/nombre-tarea
```

Después de completar y verificar el trabajo:

```powershell
git add .
git commit -m "Tarea: descripción"
git push -u origin feature/nombre-tarea
```

Crear un Pull Request hacia `develop`. El paso de `develop` a `main` debe realizarse cuando la solución compile, Docker Compose levante el ecosistema, el health agregado esté saludable y las rutas, autenticación e integración hayan sido verificadas.

## 19. Lista rápida de incorporación

- [ ] Clonar y actualizar `develop`.
- [ ] Abrir Docker Desktop.
- [ ] Copiar `.env.example` como `.env`.
- [ ] Configurar contraseñas locales y un JWT de al menos 32 caracteres.
- [ ] Ejecutar `.\scripts\deploy.ps1`.
- [ ] Confirmar los nueve contenedores con `docker compose ps`.
- [ ] Confirmar `Healthy` y `5/5` en `/health/services`.
- [ ] Ejecutar `.\scripts\test-gateway-routes.ps1`.
- [ ] Abrir RabbitMQ Management y Seq si se requieren.

