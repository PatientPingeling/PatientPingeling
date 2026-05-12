# AD: Schedulingmechanisme voor tijdgebonden notificaties

| Eigenschap       | Waarde             |
|------------------|--------------------|
| **Status**       | ✅ Accepted        |
| **Datum**        | 12-05-2026         |
| **Beslissers**   | PatientPingeling   |
| **Geraadpleegd** | -                  |

## Context en Probleembeschrijving

De communicatiemodule moet notificaties versturen op vaste tijdstippen vóór een afspraak (24 uur en 1 uur van tevoren). Deze tijdstippen worden berekend op het moment dat een afspraak binnenkomt via de webhook en opgeslagen in een `scheduled_notifications` tabel. Een achtergrondproces moet vervolgens periodiek controleren welke notificaties klaar zijn om verstuurd te worden en deze doorzetten naar RabbitMQ.

De vraag is: hoe implementeren we dit schedulingmechanisme op een betrouwbare, schaalbare en begrijpelijke manier?

## Beslissingsfactoren

- Betrouwbaarheid bij meerdere instanties (geen dubbele verzending)
- Eenvoud van implementatie en onderhoudbaarheid
- Begrijpelijkheid voor het team (elk onderdeel moet uitlegbaar zijn)
- Geen onnodige externe dependencies
- Schaalbaarheid naar de toekomst

## Overwogen Opties

1. **Raw polling met SELECT FOR UPDATE SKIP LOCKED**: Een .NET BackgroundService pollt de `scheduled_notifications` tabel periodiek. PostgreSQL's `SELECT FOR UPDATE SKIP LOCKED` zorgt ervoor dat meerdere instanties van de Scheduler nooit dezelfde rij oppikken. Geen externe dependencies buiten PostgreSQL.

2. **Hangfire**: Een .NET job scheduling library met een eigen database-backend. Biedt een dashboard, distributed locking, retry-policies en geplande jobs met een cron-achtige syntax. Vereist een extra schema in de database en een aparte Hangfire-configuratie.

3. **Quartz.NET**: Een volwaardige job scheduler voor .NET met ondersteuning voor clustering, cron-expressies en persistent job stores. Vergelijkbaar met Hangfire maar zonder ingebouwd dashboard.

## Resultaten

We hebben gekozen voor **raw polling met SELECT FOR UPDATE SKIP LOCKED** (optie 1).

**Hangfire (optie 2) is afgewezen.** Hangfire is een uitstekende library die distributed locking, dashboarding en retry-policies kant-en-klaar levert. Voor ons project introduceert het echter een dependency die we niet nodig hebben — PostgreSQL's `SELECT FOR UPDATE SKIP LOCKED` lost het distributed locking probleem al op zonder extra library. Bovendien wil het team elke keuze kunnen uitleggen: raw SQL is transparant, Hangfire verbergt de onderliggende mechanismen. Hangfire blijft een **toekomstige overweging** als het aantal job-types groeit of als een visueel scheduler-dashboard gewenst is.

**Quartz.NET (optie 3) is afgewezen** om dezelfde redenen als Hangfire. De toegevoegde waarde weegt niet op tegen de extra complexiteit voor onze huidige use-case.

De raw polling aanpak werkt als volgt:

- De Scheduler voert periodiek `SELECT ... FOR UPDATE SKIP LOCKED` uit op `scheduled_notifications` waar `send_at <= NOW()` en `status = pending`.
- PostgreSQL garandeert atomisch dat elke rij door maximaal één Scheduler-instantie tegelijk wordt opgepikt, zonder extra locking-infrastructuur.
- Na het oppikken wordt de status bijgewerkt naar `processing` en wordt een send-command gepubliceerd naar RabbitMQ.
- De Notification Worker controleert bij ontvangst of de rij nog steeds bestaat en `processing` is, als bescherming tegen late annuleringen.

### Gevolgen

- Goed, omdat `SELECT FOR UPDATE SKIP LOCKED` distributed locking gratis levert via PostgreSQL — geen extra library of infrastructuur.
- Goed, omdat het mechanisme volledig transparant en begrijpelijk is voor het team.
- Goed, omdat er geen extra dependencies worden geïntroduceerd.
- Slecht, omdat polling altijd een kleine vertraging introduceert gelijk aan het polling-interval.
- Slecht, omdat er geen ingebouwd dashboard is voor job-monitoring (wordt opgevangen door Grafana + OpenTelemetry, zie ADR-0008).
- Toekomstige overweging: Hangfire of Quartz.NET als het aantal verschillende job-types toeneemt of als een dedicated scheduler-dashboard gewenst is.

## Meer Informatie

- PostgreSQL SELECT FOR UPDATE SKIP LOCKED: https://www.postgresql.org/docs/current/sql-select.html#SQL-FOR-UPDATE-SHARE
- Gerelateerde ADRs:
  - [ADR-0004: RabbitMQ als interne queue-infrastructuur](0004-message-broker-rabbitmq.md)
  - [ADR-0005: PostgreSQL als database](0005-database-postgresql.md)
  - [ADR-0008: Observability-stack Grafana + OpenTelemetry](0008-observability-grafana-opentelemetry.md)
