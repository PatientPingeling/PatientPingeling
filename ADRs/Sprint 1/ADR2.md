---
status: Accepted
date: 24-04-2026
deciders: PatientPingeling
---

# AD: Technologie stack

## Context en Probleembeschrijving

Bij de bouw van de communicatiemodule moeten fundamentele keuzes gemaakt worden voor de programmeertaal, het framework, de berichtenwachtrij, de opslag en de monitoringtooling. Deze keuzes hebben directe invloed op de onderhoudbaarheid, schaalbaarheid, observability en de aansluiting bij de kennis van het team.

Welke technologiestack wordt gebruikt voor de communicatiemodule?

## Beslissingsfactoren

- Complexiteit beperken
- Onderhoudbaarheid verbeteren
- Schaalbaarheid ondersteunen
- Observability ondersteunen
- Aansluiten bij teamkennis en projectkaders

## Overwogen Opties

### Backend / framework

1. Node.js / JavaScript
2. C# / .NET
3. Java / Spring Boot

### Database / opslag

1. PostgreSQL
2. MySQL
3. MS SQL Server

### Message queue

1. RabbitMQ
2. Apache Kafka
3. Redis Streams

### Monitoring / dashboarding

1. Grafana
2. Kibana / OpenSearch Dashboards
3. Eigen dashboard bouwen

## Resultaten

We hebben besloten om de volgende technologiestack te gebruiken:

- Backend framework: **C# / .NET**
- Database opslag: **PostgreSQL**
- Message queue: **RabbitMQ**
- Monitoring: **Grafana**

Voor de programmeertaal en het framework kiezen we **C# / .NET**. Wij kozen hiervoor omdat C# veel overeenkomt met Java, maar beter aansluit bij de kennis van het team. .NET heeft goede ondersteuning voor Web API's, background workers, dependency injection, async/await en integraties met RabbitMQ, PostgreSQL en observability tooling.

**Node.js / JavaScript** is niet gekozen omdat het team voor deze module sterker is in C# en omdat de worker/scheduler-structuur van .NET beter past bij langdurige background processing. **Java / Spring Boot** is niet gekozen omdat OpenMRS al Java gebruikt, maar de communicatiemodule bewust losstaat van OpenMRS en het team meer snelheid verwacht met C# / .NET.

Voor de berichtenwachtrij kiezen we **RabbitMQ**. RabbitMQ past bij onze event-driven koppeling met OpenMRS en kan berichten vasthouden wanneer de communicatiemodule tijdelijk offline is. Hierdoor wordt OpenMRS niet direct afhankelijk van de beschikbaarheid van onze module of van externe messaging providers.

**Apache Kafka** is niet gekozen omdat Kafka vooral sterk is voor grote event streams en replay-scenario's, terwijl onze module vooral betrouwbare work-queue verwerking nodig heeft. **Redis Streams** is niet gekozen omdat RabbitMQ duidelijker aansluit op durable queues, acknowledgements en retry-gedrag voor deze opdracht.

Voor opslag gebruiken we een eigen PostgreSQL database binnen de notificatie module. Deze database is niet de OpenMRS database. We gebruiken PostgreSQL om een lokale projectie van afspraken, reminder-statussen en notificatiepogingen op te slaan. Hierdoor kan de notificatie module zelfstandig bepalen wanneer een reminder verstuurd moet worden, zonder de OpenMRS database rechtstreeks uit te lezen.

**MySQL** is niet gekozen omdat PostgreSQL beter aansluit bij onze behoefte aan betrouwbare relationele opslag, sterke query-mogelijkheden en flexibele opslag van event-/statusdata. **MS SQL Server** is niet gekozen omdat PostgreSQL lichter en eenvoudiger lokaal te draaien is in onze Docker Compose omgeving en geen extra licentie- of platformkeuzes introduceert.

Voor monitoring en dashboarding kiezen we **Grafana**. Grafana sluit aan op het sprintdoel waarin OpenMRS-beheerders real-time inzicht moeten krijgen in verstuurde en mislukte berichten, throughput, actieve foutmeldingen en circuit breaker-activiteit. De communicatiemodule publiceert hiervoor metrics en statusinformatie die in Grafana zichtbaar gemaakt kunnen worden.

**Kibana / OpenSearch Dashboards** is niet gekozen omdat die optie vooral sterk is voor loganalyse, terwijl onze sprint expliciet vraagt om een real-time operationeel dashboard met status, throughput en foutactiviteit. Een **eigen dashboard bouwen** is niet gekozen omdat dit meer ontwikkeltijd kost en minder snel productiewaardige monitoring oplevert dan Grafana.

De module draait als een combinatie van een **.NET Web API** en een **.NET background service**. De Web API ondersteunt voorbeeldrequests en beheer-/dashboardscenario's. De background service verwerkt berichten uit RabbitMQ en voert periodiek geplande taken uit, zoals het controleren van afspraken waarvoor 24 uur of 1 uur van tevoren een notificatie nodig is.

### Gevolgen

- Goed, omdat de module volledig afgeschermd is van de hoofdapplicatie en zeer schaalbaar is.
- Goed, omdat C# / .NET goed aansluit bij de kennis van het team en een modern ecosysteem heeft.
- Goed, omdat RabbitMQ temporal coupling tussen OpenMRS, de communicatiemodule en messaging providers verlaagt.
- Goed, omdat PostgreSQL betrouwbare opslag biedt voor afspraakprojecties, reminder-status en notificatiepogingen.
- Goed, omdat Grafana real-time inzicht geeft in betrouwbaarheid, throughput en foutscenario's.
- Goed, omdat de module na downtime opnieuw kan bepalen welke notificaties nog verstuurd moeten worden.
- Slecht, omdat developers met verschillende stacks moeten werken. Er worden 2 verschillende talen en frameworks gebruikt bij het werken aan de notificatie module met OpenMRS en C#.
- Slecht, omdat een eigen database extra beheer, migraties en dataconsistentie vraagt.
- Slecht, omdat Grafana en metrics extra configuratie en beheer toevoegen aan de Docker-omgeving.

## Meer Informatie

- Module developer documentation, how to use our message broker: https://openmrs.atlassian.net/wiki/spaces/docs/pages/25462172/For+Module+Developers
- PostgreSQL documentation: https://www.postgresql.org/docs/
- Grafana documentation: https://grafana.com/docs/grafana/latest/
- RabbitMQ documentation: https://www.rabbitmq.com/docs
