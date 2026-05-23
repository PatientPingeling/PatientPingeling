# PatientPingeling

A SaaS notification module that automatically sends appointment reminders to patients via external messaging providers. This is a monorepo containing both the standalone .NET notification service and plugins (e.g., OpenMRS Java plugin) that feed it.

---

## How it works

```
OpenMRS ──(event)──► [OpenMRS Enricher plugin] ──(HTTP webhook)──► [Notification API]
                                                                           │
                                                              ┌────────────┘
                                                              │  Stores appointment +
                                                              │  2 scheduled notifications
                                                              │  (24h + 1h before)
                                                              ▼
                                                       [Scheduler]
                                                              │  Polls DB, publishes
                                                              │  send-commands to RabbitMQ
                                                              ▼
                                                        [Worker] ──► [SwiftSend / LegacyLink
                                                                       AsyncFlow / SecurePost]
                                                                              │
                                                                              ▼
                                                                           Patient
```

1. OpenMRS fires an appointment event
2. The **OpenMRS Enricher plugin** (Java) catches it, enriches it with patient and appointment data via the OpenMRS FHIR API, and POSTs it to the Notification API as a webhook
3. The **Notification API** validates the request, stores the appointment + patient + 2 scheduled notifications (24h and 1h before) in one transaction
4. The **Scheduler** polls for due notifications and publishes send-commands to RabbitMQ
5. The **Worker** consumes send-commands, looks up the tenant's provider credentials, and dispatches the reminder via the configured messaging provider

---

## Tech stack

| Component           | Technology                                                  |
| ------------------- | ----------------------------------------------------------- |
| Notification API    | C# / .NET 10 (Minimal API)                                  |
| Scheduler           | C# / .NET 10 (Background Service)                           |
| Worker              | C# / .NET 10 (Worker Service)                               |
| Database            | PostgreSQL 18                                               |
| Message broker      | RabbitMQ 4                                                  |
| Messaging providers | FakeComWorld (SwiftSend, LegacyLink, AsyncFlow, SecurePost) |
| OpenMRS plugin      | Java 8 / Maven                                              |
| Security            | AES-256-GCM (credentials at rest), SHA-256 (webhook secret) |
| Observability       | OpenTelemetry + Grafana LGTM stack                          |
| Containerization    | Docker / Docker Compose                                     |

---

## Getting started

**Prerequisites:** [Docker](https://docs.docker.com/get-docker/) and Docker Compose

```bash
# 1. Copy and fill in environment variables
cp .env.example .env

# 2. Start the full stack
docker compose up --build
```

| Service                | URL                           |
| ---------------------- | ----------------------------- |
| Notification API       | http://localhost:8000         |
| API docs (OpenAPI)     | http://localhost:8000/openapi |
| RabbitMQ management    | http://localhost:15672        |
| Grafana dashboard      | http://localhost:3000         |
| FakeComWorld providers | http://localhost:1337         |
| PostgreSQL             | localhost:5432                |

### Test the webhook

Open the Bruno workspace in [`bruno/`](bruno/), select the `docker` environment, and run the **System Tests** collection in order:

1. `webhook CREATED` — creates patient + appointment + 2 scheduled notifications
2. `webhook UPDATED` — updates appointment and regenerates notifications if time changed
3. `webhook CANCELLED` — sets `IsCancelled = true` and deletes pending notifications
4. `webhook validation failure` — verifies 400 on invalid payload

Or use curl:

```bash
curl -X POST http://localhost:8000/webhooks/appointments \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: 3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: test-secret" \
  -d '{
    "action": "CREATED",
    "patient": { "externalId": "PP-001", "givenName": "Jan", "email": "jan@example.com", "phoneNumber": null },
    "appointment": { "externalId": "APT-001", "scheduledAt": "2027-01-01T10:00:00+01:00", "service": "General Medicine", "location": "Kamer 1", "instructions": null }
  }'
```

Expected response: `201 Created`

> **Dev tenant** is seeded automatically on startup in Development/testing environments.
> Tenant ID: `3fa85f64-5717-4562-b3fc-2c963f66afa6`, API key: `test-secret`

To reset test data:

```bash
./scripts/reset-dev-db.sh   # macOS/Linux
./scripts/reset-dev-db.ps1  # Windows
```

---

## Configuration

Copy `.env.example` to `.env` and fill in the values:

```env
# --- PatientPingeling (OpenMRS plugin) ---
# --- General ---
ASPNETCORE_ENVIRONMENT=Development

# --- Notification Service (Postgres) ---
POSTGRES_HOST=postgres
POSTGRES_DB=notificationservice
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your_secure_password_here

# --- Message Broker (RabbitMQ) ---
RABBITMQ_HOST=rabbitmq
RABBITMQ_USERNAME=guest
RABBITMQ_PASSWORD=guest

# --- OpenMRS (MariaDB) ---
OMRS_DB_USER=openmrs
OMRS_DB_PASSWORD=openmrs_secure_password
MYSQL_ROOT_PASSWORD=root_secure_password

# --- OpenMRS image tag (qa = latest stable, nightly = bleeding edge) ---
OPENMRS_TAG=qa

# --- Security ---
# Generate with: openssl rand -base64 32
ENCRYPTION_KEY=your-32-byte-base64-encoded-key-here

# --- Messaging Providers (FakeComWorld) ---
STUDENT_GROUP=PatientPingeling

# --- Observability ---
# Grafana UI is available at http://localhost:3000 after docker compose up (no login required)
# No variables needed — the endpoint is hardcoded to the internal docker network (http://otel-lgtm:4317)

# Webhook target inside docker network:
PP_WEBHOOK_URL=http://api:8000/webhooks/appointments

# Option 1: provide API key + tenant via env vars
PP_API_KEY=fill_me
PP_TENANT_KEY=fill_me

# Option 2: provide secrets via JSON file (mounted in container)
# PP_SECRETS_FILE=/run/secrets/pp-secrets.json

# Service account used by the plugin to authenticate inside OpenMRS
PP_SERVICE_USER=fill_me
PP_SERVICE_PASSWORD=fill_me
```

Provider credentials (API keys, JWT secrets etc.) are stored **encrypted in the database**, never in config files or environment variables.

---

## Architecture

The notification service follows **Clean Architecture** — dependencies always point inward:

```
Api / Scheduler / Worker
        │
        ▼
   Application  ◄──  Infrastructure
        │
        ▼
      Domain
```

**Projects:**

| Project                              | Role                                                  |
| ------------------------------------ | ----------------------------------------------------- |
| `NotificationService.Api`            | Receives webhooks, validates, stores appointments     |
| `NotificationService.Scheduler`      | Polls DB for due notifications, publishes to RabbitMQ |
| `NotificationService.Worker`         | Consumes RabbitMQ, calls messaging providers          |
| `NotificationService.Application`    | Business logic, interfaces, commands                  |
| `NotificationService.Infrastructure` | EF Core, RabbitMQ, provider HTTP clients, encryption  |
| `NotificationService.Domain`         | Entities, Result pattern, ErrorType                   |

For full architectural context, ADRs, and C4 diagrams, see [`docs/`](docs/). The database schema is documented as [Mermaid ERD](docs/ERD/database-schema.md).

---

## OpenMRS Plugin

**Prerequisites:** Java 1.8, Maven 2.x+, a running OpenMRS instance

```bash
cd plugins/OpenMRS.PatientPingeling
mvn clean package
```

Install via **OpenMRS Administration → Manage Modules**, or drop the `.omod` into `~/.OpenMRS/modules/` and restart. Configure the webhook URL and tenant credentials via OpenMRS Global Properties in the admin UI.

---

## Running tests

```bash
# Unit tests — isolated, no Docker needed (~59 tests, milliseconds)
dotnet test tests/NotificationService.UnitTests

# Architecture tests — enforce Clean Architecture rules, no Docker needed (~9 tests, milliseconds)
dotnet test tests/NotificationService.ArchTests

# Integration tests — real PostgreSQL + RabbitMQ via Testcontainers, Docker required (~12 tests)
dotnet test tests/NotificationService.IntegrationTests

# All .NET tests
dotnet test

# System tests (manual) — run Bruno against the full docker compose stack
# Open bruno/ in Bruno app, select 'docker' environment, run 'System Tests' collection

# Java plugin
cd plugins/OpenMRS.PatientPingeling
mvn test
```

---

## License

MIT — see [LICENSE](LICENSE).
