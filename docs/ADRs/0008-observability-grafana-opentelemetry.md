# AD: Observability-stack: Grafana + OpenTelemetry

| Eigenschap       | Waarde           |
| ---------------- | ---------------- |
| **Status**       | ✅ Accepted      |
| **Datum**        | 12-05-2026       |
| **Beslissers**   | PatientPingeling |
| **Geraadpleegd** | Robin Schellius  |

## Context en Probleembeschrijving

De communicatiemodule bestaat uit meerdere onderdelen: een Notification API, een BackgroundService (Scheduler), Notification Workers en RabbitMQ. Bij problemen — gemiste notificaties, queue-storingen, trage verwerking — moet het team snel kunnen zien wat er misgaat en waar. Zonder centraal inzicht in logs, metrics en traces is foutdiagnose tijdrovend en onbetrouwbaar.

ADR-0007 vermeldt expliciet dat het ontbreken van een ingebouwd scheduler-dashboard wordt opgevangen door Grafana + OpenTelemetry. Deze ADR legt de keuze voor die stack vast.

Welke observability-stack gebruiken we om de communicatiemodule te monitoren?

## Beslissingsfactoren

- Inzicht in logs, metrics en traces (de drie pijlers van observability)
- Geen extra licentiekosten of platformkeuzes
- Eenvoudig te draaien in Docker Compose (naast de bestaande services)
- Goede ondersteuning voor .NET via de OpenTelemetry SDK
- Inzicht in RabbitMQ-queue-gedrag en PostgreSQL-queryprestaties

## Overwogen Opties

1. **Grafana + OpenTelemetry (Prometheus, Loki, Tempo)**: Open-source observability-stack. OpenTelemetry instrumenteert de .NET-applicatie voor traces en metrics; Prometheus scrapt metrics; Loki verzamelt logs; Tempo slaat distributed traces op. Grafana visualiseert alles in één dashboard.
2. **Datadog**: Volledig beheerde observability-SaaS. Biedt alles out-of-the-box maar kost geld en bindt aan een externe clouddienst.
3. **ELK Stack (Elasticsearch, Logstash, Kibana)**: Krachtige log-aggregatie, maar zwaar om lokaal te draaien en minder sterk in metrics en tracing dan Grafana + OpenTelemetry.
4. **Geen dedicated observability**: Alleen console-logging en handmatige inspectie.

## Resultaten

We hebben gekozen voor **Grafana + OpenTelemetry** (optie 1).

**Datadog (optie 2) is afgewezen** omdat het een betaalde SaaS-dienst is die externe licentiekosten en een afhankelijkheid van een externe clouddienst introduceert — onwenselijk voor dit project.

**ELK Stack (optie 3) is afgewezen** omdat het zwaarder is om lokaal te draaien in Docker Compose en minder goed integreert met .NET metrics en tracing vergeleken met de OpenTelemetry + Grafana combinatie.

**Geen observability (optie 4) is afgewezen** omdat het team bij problemen (gemiste notificaties, queue-storingen) blind is en foutdiagnose onmogelijk wordt — zeker gegeven de asynchrone, multi-component architectuur.

De gekozen stack werkt als volgt:

- De .NET-services zijn geïnstrumenteerd met de **OpenTelemetry SDK** (logs, metrics, traces).
- **Prometheus** scrapt metrics-endpoints van de .NET-services en RabbitMQ.
- **Loki** verzamelt gestructureerde logs via een log-shipper.
- **Tempo** slaat distributed traces op zodat een notificatie-flow van webhook tot verstuurde notificatie gevolgd kan worden.
- **Grafana** visualiseert logs, metrics en traces in één centrale interface en vervangt zo het ontbrekende ingebouwde scheduler-dashboard (zie ADR-0007).

### Gevolgen

- Goed, omdat de volledige stack open-source en gratis is, zonder licentiekosten.
- Goed, omdat OpenTelemetry een vendor-neutrale standaard is: de instrumentatie in .NET is niet gebonden aan Grafana en kan later naar een andere backend worden gestuurd.
- Goed, omdat RabbitMQ en PostgreSQL beide native Prometheus-metrics exporteren, waardoor queue-gedrag en databaseprestaties direct zichtbaar worden in Grafana.
- Goed, omdat Grafana, Prometheus, Loki en Tempo eenvoudig als containers aan Docker Compose worden toegevoegd zonder extra externe services.
- Slecht, omdat de initiële configuratie van dashboards, alerting-rules en scrape-configs handmatig werk vereist.
- Slecht, omdat de stack geheugen- en CPU-intensiever is dan geen observability, wat op een lokale ontwikkelmachine merkbaar kan zijn.

## Meer Informatie

- [OpenTelemetry .NET SDK](https://opentelemetry.io/docs/languages/dotnet/)
- [Grafana documentatie](https://grafana.com/docs/)
- [Prometheus documentatie](https://prometheus.io/docs/)
- [Loki documentatie](https://grafana.com/docs/loki/)
- [Tempo documentatie](https://grafana.com/docs/tempo/)
- Gerelateerde ADRs:
  - [ADR-0002: Backend C# / .NET](0002-backend-csharp-dotnet.md)
  - [ADR-0007: Scheduling — polling zonder ingebouwd dashboard](0007-scheduling-select-for-update.md)
- Gerelateerde diagrammen:
  - [C4 Container](../C4/C4_Container.drawio)
