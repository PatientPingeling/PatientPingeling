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

Use the reference payload in [`webhook.json`](webhook.json):

```bash
curl -X POST http://localhost:8000/webhooks/appointments \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: 00000000-0000-0000-0000-000000000001" \
  -H "X-Webhook-Secret: your-secret-here" \
  -d @webhook.json
```

Expected response: `201 Created`

---

## Configuration

Copy `.env.example` to `.env` and fill in the values:

```env
ASPNETCORE_ENVIRONMENT=Development

# PostgreSQL
POSTGRES_HOST=postgres
POSTGRES_DB=notificationservice
POSTGRES_USER=postgres
POSTGRES_PASSWORD=yourpassword

# RabbitMQ
RABBITMQ_HOST=rabbitmq
RABBITMQ_USERNAME=guest
RABBITMQ_PASSWORD=guest

# Messaging providers
STUDENT_GROUP=PatientPingeling
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
# .NET
dotnet test

# Java plugin
cd plugins/OpenMRS.PatientPingeling
mvn test
```

---

## License

MIT — see [LICENSE](LICENSE).
