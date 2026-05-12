# PatientPingeling — Documentation

This repository contains all architectural and design documentation for the **OpenMRS Notification Service** project, built under the **PatientPingeling** initiative.

---

## What is this project?

The OpenMRS Notification Service is a standalone service that integrates with [OpenMRS](https://openmrs.org/) to send automated appointment reminders to patients. It is designed as a separate, independently deployable service that receives appointment events via RabbitMQ.

On the OpenMRS side, we can optionally add a small OpenMRS module (plugin) to enrich and publish events to RabbitMQ (e.g. `patientpingeling-enricher_module`).

---

## Repository structure

```
Docs/
├── ADRs/                        # Architecture Decision Records
│   ├── template.md              # Template for new ADRs
│   ├── Sprint 1/
│   │   ├── ADR1.md              # Architecture style: standalone module
│   │   ├── ADR2.md              # Technology stack: .NET, PostgreSQL, RabbitMQ
│   │   └── ADR3.md              # Integration method: event-driven via RabbitMQ
│   └── Sprint 2/
│       ├── ADR4.md              # Queue infrastructure: RabbitMQ chosen over alternatives
│       └── ADR5.md              # Database: PostgreSQL for the notification service
│   └── Sprint 3/
│       ├── ADR6.md              # Monitoring: Grafana dashboards
│       └── ADR7.md              # OpenMRS enricher module publishes to RabbitMQ
├── C4/                          # C4 model diagrams (DrawIO format)
│   ├── C4_Context.drawio        # System context diagram
│   └── C4_Container.drawio      # Container/component diagram
├── Questions.md                 # Open architectural questions
└── README.md
```

---

## Architecture Decision Records (ADRs)

ADRs document every significant architectural choice made during the project, including the context, the considered alternatives, and the rationale behind the decision.

| ADR                             | Title                                            | Sprint   |
| ------------------------------- | ------------------------------------------------ | -------- |
| [ADR1](ADRs/Sprint%201/ADR1.md) | Standalone module architecture                   | Sprint 1 |
| [ADR2](ADRs/Sprint%201/ADR2.md) | Technology stack (.NET 10, PostgreSQL, RabbitMQ) | Sprint 1 |
| [ADR3](ADRs/Sprint%201/ADR3.md) | Event-driven integration via RabbitMQ            | Sprint 1 |
| [ADR4](ADRs/Sprint%202/ADR4.md) | RabbitMQ as queue infrastructure                 | Sprint 2 |
| [ADR5](ADRs/Sprint%202/ADR5.md) | PostgreSQL database for the notification service | Sprint 2 |
| [ADR6](ADRs/Sprint%203/ADR6.md) | Monitoring and dashboarding via Grafana          | Sprint 3 |
| [ADR7](ADRs/Sprint%203/ADR7.md) | OpenMRS enricher module publishes to RabbitMQ    | Sprint 3 |

To add a new ADR, copy [the template](ADRs/template.md) and place it in the relevant sprint folder.

---

## C4 Diagrams

The [C4 folder](C4/) contains system architecture diagrams following the [C4 model](https://c4model.com/) standard. Open the `.drawio` files with [draw.io](https://app.diagrams.net/) or the VS Code Draw.io extension.

| Diagram               | Description                                                     |
| --------------------- | --------------------------------------------------------------- |
| `C4_Context.drawio`   | High-level view: the system and its external actors             |
| `C4_Container.drawio` | Containers: API, message broker, database, and how they connect |

---

## Key architectural decisions at a glance

- **Standalone module** — the service runs independently of OpenMRS for better scalability and separation of concerns.
- **C# / .NET 10** — backend language and framework.
- **PostgreSQL** — relational database for persisting notification state.
- **RabbitMQ** — message broker for receiving appointment events from OpenMRS asynchronously.
- **FHIR compliance** — integration design is aligned with the HL7 FHIR standard.
- **Horizontal scalability** — the service supports multiple concurrent OpenMRS instances.

---

## License

MIT — see [LICENSE](LICENSE).
