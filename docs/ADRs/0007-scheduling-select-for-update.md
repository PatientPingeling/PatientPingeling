# AD: Schedulingmechanisme voor tijdgebonden notificaties

| Eigenschap       | Waarde           |
| ---------------- | ---------------- |
| **Status**       | ✅ Accepted      |
| **Datum**        | 12-05-2026       |
| **Beslissers**   | PatientPingeling |
| **Geraadpleegd** | -                |


> [!WARNING]
> Deze beslissing is herzien. De notification-status wordt nu bijgehouden via de `dispatch_logs` tabel. De worker gebruikt de nieuwste dispatch logregel om idempotentie en verwerkingstoestand te bepalen.

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

1. **Raw polling met dispatch log als statusbron**: Een .NET BackgroundService pollt de `scheduled_notifications` tabel periodiek op basis van `send_at`. De actuele verwerkingsstatus wordt niet apart in `scheduled_notifications` opgeslagen, maar afgeleid uit de `dispatch_logs` tabel. De worker controleert bij ontvangst van een RabbitMQ-bericht of de nieuwste dispatch log voor dezelfde `scheduled_notification_id` al `SUCCESS` is. Als dat zo is, wordt het bericht als duplicate genegeerd.

2. **Hangfire**: Een .NET job scheduling library met een eigen database-backend. Biedt een dashboard, distributed locking, retry-policies en geplande jobs met een cron-achtige syntax. Vereist een extra schema in de database en een aparte Hangfire-configuratie.

3. **Quartz.NET**: Een volwaardige job scheduler voor .NET met ondersteuning voor clustering, cron-expressies en persistent job stores. Vergelijkbaar met Hangfire maar zonder ingebouwd dashboard.

## Resultaten

We hebben gekozen voor **Raw polling met dispatch log als statusbron** (optie 1).

**Hangfire (optie 2) is afgewezen.** Hangfire is een uitstekende library die dashboarding en retry-policies kant-en-klaar levert. Voor ons project introduceert het echter een dependency die we niet nodig hebben. Bovendien wil het team elke keuze kunnen uitleggen: raw SQL is transparant, Hangfire verbergt de onderliggende mechanismen. Hangfire blijft een **toekomstige overweging** als het aantal job-types groeit of als een visueel scheduler-dashboard gewenst is.

**Quartz.NET (optie 3) is afgewezen** om dezelfde redenen als Hangfire. De toegevoegde waarde weegt niet op tegen de extra complexiteit voor onze huidige use-case.

De raw polling aanpak werkt als volgt:

- De Scheduler voert periodiek een query uit op `scheduled_notifications` waar `send_at <= NOW()`.
- Na het oppikken wordt een send-command gepubliceerd naar RabbitMQ.
- De Notification Worker behandelt de `dispatch_logs` tabel als bron van waarheid voor verwerkingstoestand en idempotentie: een bestaande nieuwste `SUCCESS`-log voor dezelfde `scheduled_notification_id` betekent dat het bericht al verwerkt is.
- Bij failures worden nieuwe `dispatch_logs` geschreven met een passende `Outcome` zoals `ERROR_TRANSIENT`, `ERROR_PERMANENT`, `EXPIRED` of `PENDING_ASYNC`.

### Gevolgen

- Goed, omdat de status van een notification expliciet traceerbaar is via `dispatch_logs` in plaats van verborgen in een extra statuskolom.
- Goed, omdat het mechanisme volledig transparant en begrijpelijk is voor het team.
- Goed, omdat er geen extra dependencies worden geïntroduceerd.
- Slecht, omdat polling altijd een kleine vertraging introduceert gelijk aan het polling-interval.
- Slecht, omdat er geen ingebouwd dashboard is voor job-monitoring (wordt opgevangen door Grafana + OpenTelemetry, zie [ADR-0008](0008-observability-grafana-opentelemetry.md)).
- Nadeel, omdat de notification-status niet direct in één veld staat maar uit de nieuwste dispatch log moet worden afgeleid.
- Toekomstige overweging: Hangfire of Quartz.NET als het aantal verschillende job-types toeneemt of als een dedicated scheduler-dashboard gewenst is.

## Meer Informatie

- Gerelateerde ADRs:
  - [ADR-0003: RabbitMQ als interne queue-infrastructuur](0003-message-broker-rabbitmq.md)
  - [ADR-0005: PostgreSQL als database](0005-database-postgresql.md)
  - [ADR-0008: Observability-stack Grafana + OpenTelemetry](0008-observability-grafana-opentelemetry.md)
