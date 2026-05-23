# Realisatie Logboek van Daniël

In dit logboek wordt beschreven welke tools zijn gebruikt tijdens de ontwikkeling van het systeem, waarom deze zijn gebruikt en wordt er gereflecteerd op de toegevoegde waarde & kosten van deze tools.

## Gebruikte ontwikkeltools (IDE's)

| Tool           | Waarom                                                                                                  | Reflectie                                                                                                                    |
| -------------- | ------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| VS Code        | Primaire code-editor voor .NET development                                                              | Lichtgewicht en snel; werkte goed met de juiste extensies. Praktische keuze gezien de hardware-beperkingen van mijn machine. |
| Rider          | Geprobeerd als volwaardige .NET IDE                                                                     | Te zwaar voor mijn MacBook Pro 2019 (8GB RAM, Intel i5) — crashte of was te traag naast Docker. Uitgeweken naar VS Code.     |
| Docker Compose | De volledige lokale stack opstarten: RabbitMQ, PostgreSQL én de eigen services (API, Scheduler, Worker) | Enorm krachtig — één commando voor de hele omgeving. `.env` secret loading was even puzzelen, maar daarna soepel in gebruik. |
| DBeaver        | PostgreSQL inspecteren: data controleren, schema bekijken, queries draaien                              | Onmisbaar naast de code om te verifiëren of migraties en inserts echt deden wat je verwachtte.                               |
| Git            | Versiebeheer: branches, commits, merges, PRs, CI/CD, issues                                             | Volledig benut als team. Goede workflow met feature branches en pull requests.                                               |
| GitHub         | Remote repository, project board, CI/CD pipelines, issue tracking                                       | Centraal samenwerkingspunt voor het team. CI/CD zorgde voor automatische checks op elke PR.                                  |
| Bruno          | API-requests handmatig testen en een integratietestcollectie bijhouden                                  | Fijn alternatief voor Postman — lokaal, geen account nodig, en de collectie staat gewoon in de repo naast de code.           |
| Draw.io        | C4 Context- en Containerdiagrammen tekenen voor de architectuurdocumentatie                             | Visueel en makkelijk aan te passen; goed voor het communiceren van de architectuur naar het team en de docent.               |
| Mermaid        | Diagrammen als code schrijven in Markdown (o.a. ERD)                                                    | Handig omdat diagrammen in de repo leven naast de code en automatisch renderen op GitHub.                                    |
| Markdown       | Alle documentatie schrijven: ADRs, README, logboeken, rapporten                                         | Simpel en effectief; alles in één formaat houdt de documentatie consistent en versiebeheerbaar.                              |

## Gebruikte AI tools

| Tool        | Waarom                                                                                                                                            | Reflectie                                                                                                                                                                                                                                                                                                |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Claude Code | Architectuur fix, sparren over ontwerpkeuzes, code generatie, en repetitieve taken (bestandsnamen, klassenamen, OTel-setup, Docker Compose setup) | In het begin veel waarde gehaald uit het sparren: architectuurvragen doordenken en concepten leren begrijpen. Naarmate de deadline naderde meer ingezet voor directe code generatie. Eerlijk gezegd soms te snel gegenereerde code overgenomen zonder het volledig te doorgronden — dat is een leerpunt. |
| Codex       | Codebase auditen op correctheid — als tweede mening naast Claude                                                                                  | Nuttig als aanvullende check; Claude en Codex gaven soms andere invalshoeken op dezelfde code, wat hielp om blinde vlekken te spotten.                                                                                                                                                                   |

## Kosten en Toegevoegde Waarde AI-Tools

| Tool          | Kosten                                     | Toegevoegde Waarde(s)                                                                                                                                                    |
| ------------- | ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Claude (Code) | ~€21,97/maand (Pro abonnement)             | Tijdsbesparing op repetitieve taken (bestandsnamen, klassen verplaatsen, CI/CD); hulp bij onbekende concepten (.NET RabbitMQ-connectie, OTEL, HTTP resilience via Polly) |
| Codex         | Eenmalig gebruikt (inbegrepen bij ChatGPT) | Als tweede mening naast Claude                                                                                                                                           |

## Voorbeelden AI-tooling gebruik

- CI/CD pipelines consolideren: drie identieke workflows samengevoegd tot één, met uitleg over waarom de tests drie keer draaiden
- RabbitMQ .NET-connectie opzetten: concept uitgelegd gekregen (exchange, queue, binding) voordat de implementatie werd geschreven
- HTTP resilience pipeline (Polly): exponential backoff, jitter en circuit breaker uitgelegd en geconfigureerd voor de message providers
- Repetitieve taken zoals bestandsnamen aanpassen, klassen naar de juiste laag verplaatsen en boilerplate aanvullen

## Verbeterpunten

Na het reflecteren op de gebruikte tools, zijn een aantal verbeterpunten naar voren gekomen:

- Gegenereerde code vaker eerst doorgronden voordat je het overneemt — zeker onder tijdsdruk was de neiging groot om snel te accepteren
- AI eerder inzetten om een concept te _begrijpen_, en daarna pas om het te implementeren; niet andersom
- Bij twijfel over gegenereerde code: zelf een minitest schrijven om te verifiëren dat het doet wat je denkt

## Bijdrage aan Project (Commits)

_Navigeer ook naar https://github.com/orgs/PatientPingeling/projects/2/views/1 en https://github.com/PatientPingeling/PatientPingeling/commits/main/ voor de op GitHub bijgehouden Project en de gehele commit geschiedenis_

| Issue/Onderdeel                                                                                                | Beschrijving                                                                                                  | Datum                   |
| -------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- | ----------------------- |
| [Projectopzet & monorepo](https://github.com/PatientPingeling/PatientPingeling/commit/9ba4d10)                 | Initiële projectstructuur opgezet: monorepo config, .gitignore, Dependabot, CI workflow                       | 2026-05-06              |
| [ADRs & C4 diagrammen](https://github.com/PatientPingeling/PatientPingeling/commit/ad717d1)                    | ADR1 t/m ADR6 geschreven; C4 Context- en Containerdiagram toegevoegd                                          | 2026-05-06 / 2026-05-09 |
| [Docker Compose & services](https://github.com/PatientPingeling/PatientPingeling/commit/706ac3a)               | Dockerfiles voor API, Scheduler en Worker; docker-compose met RabbitMQ, PostgreSQL en alle services           | 2026-05-11              |
| [Domeinentiteiten & EF Core](https://github.com/PatientPingeling/PatientPingeling/commit/a55ffae)              | Domeinmodel opgezet (Patient, Appointment, Notification, Tenant etc.); EF Core DbContext en initiële migratie | 2026-05-12              |
| [AES encryptie](https://github.com/PatientPingeling/PatientPingeling/commit/ca62dba)                           | AES-GCM encryptie- en decryptieservice geïmplementeerd voor provider credentials                              | 2026-05-12              |
| [OpenTelemetry & Grafana LGTM](https://github.com/PatientPingeling/PatientPingeling/commit/7ef7123)            | OTel wired met Grafana LGTM stack; Grafana dashboards en datasources geconfigureerd                           | 2026-05-13 / 2026-05-21 |
| [Webhook ingestion](https://github.com/PatientPingeling/PatientPingeling/commit/c7f6b3e)                       | AppointmentIngestionService met CREATED/UPDATED/CANCELLED handlers; transactionele UnitOfWork                 | 2026-05-15              |
| [TenantService & API key validatie](https://github.com/PatientPingeling/PatientPingeling/commit/ccf13e0)       | API key validatie via X-Api-Key header; TenantService en webhook endpoint volledig gewired                    | 2026-05-15              |
| [Message providers & resilience](https://github.com/PatientPingeling/PatientPingeling/commit/60ac35f)          | HTTP clients voor alle providers; exponential backoff, jitter en circuit breaker via Polly                    | 2026-05-21              |
| [Security fixes (audit)](https://github.com/PatientPingeling/PatientPingeling/commit/b82d70e)                  | PBKDF2 voor API key hashing, XML injection fix, AES key length validatie bij startup                          | 2026-05-21              |
| [Correctheid & stabiliteitsfixes (audit)](https://github.com/PatientPingeling/PatientPingeling/commit/11a27ea) | N+1 query fix, idempotency check worker, RabbitMQ automatic recovery, null-guards webhook                     | 2026-05-21              |
| [GDPR & data retention](https://github.com/PatientPingeling/PatientPingeling/commit/1b713d6)                   | DataRetentionService: patiëntdata na 14 dagen verwijderen, notificatielogs na 1 jaar opruimen                 | 2026-05-22              |
| [AsyncFlow status polling](https://github.com/PatientPingeling/PatientPingeling/commit/873443e)                | PENDING_ASYNC status bij submit; bevestiging van bezorging via scheduler polling                              | 2026-05-22              |
| [Auditrapport & compliance](https://github.com/PatientPingeling/PatientPingeling/commit/f696355)               | Compliance sectie in auditrapport; functionele en niet-functionele requirements gedocumenteerd                | 2026-05-22              |
