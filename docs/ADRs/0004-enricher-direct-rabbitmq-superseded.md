# AD: OpenMRS data-enrichment module publiceert naar RabbitMQ

| Eigenschap       | Waarde                                                       |
| ---------------- | ------------------------------------------------------------ |
| **Status**       | ⚠️ Superseded door [ADR-0006](0006-enricher-http-webhook.md) |
| **Datum**        | 11-05-2026                                                   |
| **Beslissers**   | PatientPingeling                                             |
| **Geraadpleegd** | -                                                            |

> [!WARNING]
> Deze beslissing is herzien. De directe RabbitMQ-verbinding van OpenMRS naar de interne broker is afgewezen omdat het interne infrastructuur blootstelt aan een extern systeem. Zie [ADR-0006](0006-enricher-http-webhook.md) voor de vervangende beslissing (HTTP webhook via Enricher Module).

## Context en Probleembeschrijving

Onze notificatieservice is een zelfstandige service (los van OpenMRS) en ontvangt afspraakgerelateerde events via RabbitMQ. We zijn van plan om de OpenMRS module `Event module` te gebruiken om te kijken wanneer afspraak data verandert. Via deze events weten we wanneer we notificaties moeten gaan sturen naar patiëten. Echter, geeft de event module alleen afspraak UUID mee. Als we alleen de UUID naar een patiënt sturen als notificatie kan hij/zij hier natuurlijk niks mee. We moeten dus nog de informatie over afspraak ophalen uit het OpenMRS systeem voordat het binnenkomt bij ons systeem.

## Beslissingsfactoren

- Betrouwbaarheid en eenvoud van delivery naar RabbitMQ
- Scheiding van verantwoordelijkheden (OpenMRS vs. notificatieservice)
- FHIR-alignment (o.a. Appointment gerelateerde data)
- Deployment/operationele impact op OpenMRS-instanties
- Beveiliging en minimale exposure van OpenMRS data

## Overwogen Opties

1. **Geen extra OpenMRS-module**: OpenMRS publiceert "ruwe" events (event-module), verrijking gebeurt downstream in de notificatieservice.
2. **OpenMRS-module patientpingeling-enricher_module**: een plugin in OpenMRS die events consumeert/observeert, verrijkt, en vervolgens publiceert naar RabbitMQ.
3. **Verrijking via API-calls vanuit de notificatieservice**: de service consumeert minimale events en haalt ontbrekende context op via OpenMRS (FHIR) REST calls.

## Resultaten

We kozen voor **Optie 2: een OpenMRS-module patientpingeling-enricher_module die verrijkt en publiceert naar RabbitMQ**.

Deze optie is gekozen omdat:

- Verrijking dicht bij de bron plaatsvindt, waardoor de event-payload richting RabbitMQ consistenter en vollediger is.
- De notificatieservice hierdoor eenvoudiger blijft (minder OpenMRS-specifieke kennis en minder extra API-calls).
- We de integratie naar RabbitMQ expliciet kunnen beheren (routing keys, exchange/queue afspraken) vanuit één duidelijke producer.

Optie 1 is minder geschikt omdat de notificatieservice dan OpenMRS-specifieke verrijkingslogica moet bevatten.
Optie 3 is minder geschikt omdat dit extra afhankelijkheid en latency introduceert (veel FHIR API-calls), en error-handling/ratelimiting complexer maakt.

### Gevolgen

- Goed, omdat RabbitMQ berichten vanaf één duidelijke producer ontvangt met een afgesproken schema.
- Goed, omdat de notificatieservice minder koppeling heeft met OpenMRS interne event-details.
- Goed, omdat verrijking/mapping dichter bij de bron beheerd kan worden.
- Slecht, omdat iedere OpenMRS-instantie de module moet installeren, configureren en onderhouden.
- Slecht, omdat foutdiagnose deels in OpenMRS-land terechtkomt (logging/monitoring voor de module).

## Meer Informatie

- Gerelateerde ADRs:
  - [ADR-0003: RabbitMQ als queue infrastructure](0003-message-broker-rabbitmq.md)
  - [ADR-0006: Vervangende beslissing — Enricher Module via HTTP webhook](0006-enricher-http-webhook.md)
- Gerelateerde diagrammen:
  - [C4 Context](../C4/C4_Context.drawio)
  - [C4 Container](../C4/C4_Container.drawio)
- OpenMRS module developer docs:
  - https://openmrs.atlassian.net/wiki/spaces/docs/pages/25462172/For+Module+Developers
- HL7 FHIR R4 Appointment resource:
  - https://hl7.org/fhir/R4/appointment.html
