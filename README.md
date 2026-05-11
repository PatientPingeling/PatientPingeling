# PatientPingeling

An event-driven system that sends automated appointment reminders to patients via [OpenMRS](https://openmrs.org/). This is a monorepo containing both the standalone notification service and the OpenMRS plugin that feeds it.

---

## How it works

```
OpenMRS ──(event)──► [OpenMRS.PatientPingeling plugin] ──(enriched msg)──► RabbitMQ ──► [Notification Service] ──► Patient
```

1. OpenMRS fires an appointment event
2. The **OpenMRS plugin** (Java) catches it, enriches it with full patient and appointment data via the OpenMRS Service Layer, and publishes a rich message to RabbitMQ
3. The **Notification Service** (.NET) consumes that message and dispatches the reminder through one or more configurable message providers

The two components are deliberately decoupled — the notification service has no direct access to OpenMRS or its database.

---

## Tech stack

| Component            | Technology              |
| -------------------- | ----------------------- |
| Notification Service | C# / .NET 10            |
| Database             | PostgreSQL 18           |
| Message broker       | RabbitMQ 4              |
| OpenMRS plugin       | Java 8 / Maven          |
| API docs             | OpenAPI (built-in .NET) |
| Observability        | Grafana + OpenTelemetry |
| Containerization     | Docker / Docker Compose |

---

## Getting started

### Notification Service (.NET)

**Prerequisites:** [Docker](https://docs.docker.com/get-docker/) and Docker Compose, or [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
# 1. Copy and fill in environment variables
cp .env.example .env

# 2. Start the full stack (API + PostgreSQL + RabbitMQ)
docker compose up --build
```

| Service             | URL                           |
| ------------------- | ----------------------------- |
| API                 | http://localhost:8080         |
| API docs (OpenAPI)  | http://localhost:8080/openapi |
| RabbitMQ management | http://localhost:15672        |
| PostgreSQL          | localhost:5432                |

```bash
# Or run locally without Docker
dotnet restore
dotnet run --project src/NotificationService.Api
```

### OpenMRS Plugin (Java)

**Prerequisites:** Java 1.8, Maven 2.x+, a running OpenMRS instance

```bash
cd plugins/OpenMRS.PatientPingeling/patientpingeling.enricher

# Build the .omod file
mvn clean package
```

Install via **OpenMRS Administration → Manage Modules**, or drop the `.omod` into `~/.OpenMRS/modules/` and restart OpenMRS. Configure the RabbitMQ connection via OpenMRS Global Properties in the admin UI.

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
RABBITMQ_PORT=5672
```

RabbitMQ credentials for the OpenMRS plugin are configured through OpenMRS Global Properties — never hardcoded.

---

## Running tests

```bash
# .NET tests
dotnet test

# Java plugin tests
cd plugins/OpenMRS.PatientPingeling/patientpingeling.enricher
mvn test
```

---

## Architecture

The notification service follows **Clean Architecture (Onion)** — dependencies always point inward.

```
┌─────────────────────────────────────────┐
│              Presentation               │
│  ┌───────────────────────────────────┐  │
│  │           Infrastructure          │  │
│  │  ┌─────────────────────────────┐  │  │
│  │  │        Application          │  │  │
│  │  │  ┌───────────────────────┐  │  │  │
│  │  │  │        Domain         │  │  │  │
│  │  │  └───────────────────────┘  │  │  │
│  │  └─────────────────────────────┘  │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

**Dependency flow:** `Api`/`Listener`/`Worker` → `Application` → `Domain` ← `Infrastructure`

The OpenMRS plugin follows a two-layer structure: `api/` holds all business logic and RabbitMQ publishing; `omod/` handles module bootstrapping and the admin UI. RabbitMQ logic never leaks into `omod/`.

For full architectural context, ADRs, and C4 diagrams, see the [Docs repository](https://github.com/PatientPingeling/Docs).

---

## License

MIT — see [LICENSE](LICENSE).
