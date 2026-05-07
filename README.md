# OpenMRS Notification Service

A standalone service that sends automated appointment reminders to patients by integrating with [OpenMRS](https://openmrs.org/) through an event-driven message queue.

---

## Overview

This service listens for appointment events published by OpenMRS via **RabbitMQ**, processes them, and dispatches notifications to patients through one or more configurable message providers. It is designed to run independently of OpenMRS — no embedded module, no direct database access — keeping a clean separation of concerns.

**Tech stack**

| Layer            | Technology              |
| ---------------- | ----------------------- |
| Backend          | C# / .NET 10            |
| Database         | PostgreSQL 18           |
| Message broker   | RabbitMQ 4.3            |
| API docs         | OpenAPI (built-in .NET) |
| Observability    | Grafana + OpenTelemetry |
| Containerization | Docker / Docker Compose |

---

## Repository structure

```
openmrs-notification-service/
├── docker-compose.yml           # Full local stack (API + PostgreSQL + RabbitMQ)
├── .env.example                 # Environment variable template
├── frontend/                    # Frontend placeholder (not yet implemented)
└── backend/
    ├── Dockerfile               # Multi-stage production build
    ├── NotificationService.slnx # .NET solution file
    ├── src/
    │   ├── NotificationService.Domain/         # Entities, value objects — no dependencies
    │   ├── NotificationService.Application/    # Use cases, interfaces, DTOs
    │   ├── NotificationService.Infrastructure/ # EF Core, RabbitMQ, message providers
    │   └── NotificationService.Api/            # ASP.NET Core Web API, composition root
    └── tests/
        ├── NotificationService.Api.Tests/
        ├── NotificationService.Core.Tests/
        └── NotificationService.Integration.Tests/
```

---

## Getting started

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and Docker Compose
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for local development without Docker)

### Run with Docker Compose

```bash
# 1. Copy and fill in environment variables
cp .env.example .env

# 2. Start the full stack
docker compose up --build
```

| Service             | URL                           |
| ------------------- | ----------------------------- |
| API                 | http://localhost:8080         |
| API docs (OpenAPI)  | http://localhost:8080/openapi |
| RabbitMQ management | http://localhost:15672        |
| PostgreSQL          | localhost:5432                |

### Run locally (without Docker)

```bash
cd backend

# Restore dependencies
dotnet restore

# Run the API
dotnet run --project src/NotificationService.Api
```

Make sure a PostgreSQL instance and RabbitMQ broker are reachable and the environment variables below are configured.

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

---

## Running tests

```bash
cd backend

# All tests
dotnet test

# Specific project
dotnet test tests/NotificationService.Core.Tests
```

---

## Architecture

The service follows **Clean Architecture (Onion)**, with dependencies always pointing inward — outer layers know about inner layers, never the reverse.

```
┌─────────────────────────────────────────┐
│              Api (Presentation)         │
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

| Layer | Project | Responsibility |
| --- | --- | --- |
| Domain | `NotificationService.Domain` | Entities and value objects. Zero external dependencies. |
| Application | `NotificationService.Application` | Use cases, service interfaces (`IMessageProvider`), and DTOs. Depends only on Domain. |
| Infrastructure | `NotificationService.Infrastructure` | EF Core (PostgreSQL), RabbitMQ consumer, and pluggable `IMessageProvider` implementations. Depends on Application. |
| Presentation | `NotificationService.Api` | Minimal ASP.NET Core endpoints and composition root. Wires Application + Infrastructure via DI. |

**Dependency flow:** `Api` → `Application` → `Domain` ← `Infrastructure`

For full architectural context, including Architecture Decision Records and C4 diagrams, see the [Docs repository](https://github.com/PatientPingeling/Docs).

---

## License

MIT — see [LICENSE](LICENSE).
