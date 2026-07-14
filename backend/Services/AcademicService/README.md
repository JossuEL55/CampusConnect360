# AcademicService

Microservicio académico de CampusConnect360, implementado en ASP.NET Core 10.

## Funcionalidad

- Registro, consulta paginada, búsqueda y actualización de estudiantes.
- Matrícula única por estudiante y año lectivo.
- Historial de matrículas y eventos académicos.
- Persistencia aislada en el esquema PostgreSQL `academic` mediante EF Core.
- Publicación de `StudentEnrolled` y `StudentStatusUpdated` en `campus.events`.
- Consumo idempotente de `PaymentConfirmed` desde `academic.payment-confirmed`.
- Outbox transaccional para publicación al menos una vez y `processed_events` para deduplicación.
- Contratos AMQP persistentes y Dead Letter Exchange para mensajes inválidos.
- Correlación, logging estructurado, health check y respuestas Problem Details.

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/academic/students` | Registra un estudiante |
| GET | `/api/academic/students?q=&page=1&pageSize=20` | Busca y pagina estudiantes |
| GET | `/api/academic/students/{id}` | Obtiene un estudiante |
| PUT | `/api/academic/students/{id}` | Actualiza un estudiante |
| POST | `/api/academic/enrollments` | Matricula un estudiante |
| GET | `/api/academic/students/{id}/enrollments` | Lista sus matrículas |
| GET | `/api/academic/students/{id}/events` | Lista su historial de eventos |
| GET | `/api/academic/health` | Estado del servicio |

La especificación OpenAPI se publica en `/swagger/v1/swagger.json` y la interfaz Swagger UI en `/swagger`.

`appsettings.json` contiene únicamente valores no sensibles y deja vacías las credenciales. Para Docker, cada integrante debe copiar `.env.example` a `.env` y definir allí sus valores; `docker-compose.yml` los inyecta mediante `Database__UserName`, `Database__Password`, `RabbitMq__UserName` y `RabbitMq__Password`. El archivo `.env` no debe incluirse en Git.

En una instalación nueva, RabbitMQ crea el usuario indicado en `.env` y el servicio de inicialización `rabbitmq-init` importa automáticamente exchanges, colas y bindings desde `infrastructure/rabbitmq/definitions.json`. Ese archivo no contiene usuarios, contraseñas ni hashes.
