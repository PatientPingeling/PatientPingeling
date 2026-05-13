# AD: OpenMRS Enricher Module verstuurt HTTP webhook naar Notification API

| Eigenschap       | Waarde           |
| ---------------- | ---------------- |
| **Status**       | ✅ Accepted      |
| **Datum**        | 11-05-2026       |
| **Beslissers**   | PatientPingeling |
| **Geraadpleegd** | Marc Mathijssen  |

## Context en Probleembeschrijving

Onze notificatieservice is een zelfstandige service (los van OpenMRS) en moet afspraakgerelateerde data ontvangen vanuit OpenMRS. De OpenMRS `Event module` signaleert wijzigingen in afspraken, maar geeft alleen een UUID mee. We moeten de volledige afspraakdata (patiëntgegevens, tijd, locatie, instructies) ophalen en verrijken voordat deze ons systeem binnenkomt.

Daarnaast speelt de systeemgrens een belangrijke rol: hoe verloopt de overdracht van OpenMRS naar de communicatiemodule op een veilige en goed gedefinieerde manier?

## Beslissingsfactoren

- Betrouwbaarheid en eenvoud van delivery naar de communicatiemodule
- Scheiding van verantwoordelijkheden (OpenMRS vs. notificatieservice)
- FHIR-alignment (o.a. Appointment gerelateerde data)
- Deployment/operationele impact op OpenMRS-instanties
- Beveiliging en expliciete systeemgrens (interne infrastructuur niet blootstellen aan externe systemen)

## Overwogen Opties

1. **Geen extra OpenMRS-module**: OpenMRS publiceert ruwe events (event-module), verrijking gebeurt downstream in de notificatieservice via extra FHIR API-calls.
2. **OpenMRS-module patientpingeling-enricher_module verstuurt HTTP webhook**: een plugin in OpenMRS die events observeert, verrijkt naar FHIR-formaat, en het resultaat via HTTP POST naar het webhook-endpoint van de Notification API stuurt.
3. **OpenMRS-module publiceert rechtstreeks naar RabbitMQ**: dezelfde plugin, maar de verrijkte payload gaat direct naar de interne RabbitMQ broker van de communicatiemodule via AMQP.

## Resultaten

We kiezen voor **Optie 2: patientpingeling-enricher_module die verrijkt en via HTTP webhook verstuurt naar de Notification API**.

Optie 3 (directe RabbitMQ-verbinding) is expliciet afgewezen omdat het de interne message broker blootstelt aan een extern systeem. Een message broker is interne infrastructuur — vergelijkbaar met een database — en mag niet bereikbaar zijn voor externe partijen. Er is geen validatie op berichtinhoud of verzender mogelijk op AMQP-niveau, wat het risico op misbruik of corruptie van de interne queue vergroot. Dit is de reden waarom ADR-0004 is vervangen door deze beslissing.

Optie 2 is gekozen omdat:

- Verrijking dicht bij de bron plaatsvindt, waardoor de payload richting de Notification API consistent en volledig is.
- De Notification API het binnenkomende verzoek kan authenticeren (bijv. via een API-key header) voordat data wordt opgeslagen.
- De systeemgrens expliciet is: OpenMRS communiceert via een publiek HTTP-endpoint, niet via directe toegang tot interne infrastructuur.
- De notificatieservice eenvoudiger blijft (geen OpenMRS-specifieke event-logica intern).

Optie 1 is minder geschikt omdat de notificatieservice dan OpenMRS-specifieke verrijkingslogica moet bevatten en veel extra FHIR API-calls moet doen, wat latency en foutgevoeligheid verhoogt.

### Gevolgen

- Goed, omdat de Notification API inkomende webhooks kan valideren en afwijzen bij onbevoegde toegang.
- Goed, omdat de notificatieservice minder koppeling heeft met OpenMRS interne event-details.
- Goed, omdat verrijking/mapping dichter bij de bron beheerd kan worden.
- Goed, omdat RabbitMQ volledig intern blijft en niet geconfigureerd hoeft te worden voor externe toegang.
- Slecht, omdat iedere OpenMRS-instantie de module moet installeren, configureren en het webhook-endpoint moet kennen.
- Slecht, omdat foutdiagnose deels in OpenMRS-land terechtkomt (logging/monitoring voor de Enricher Module).

## Meer Informatie

- Gerelateerde ADRs:
  - [ADR-0003: RabbitMQ als interne queue infrastructure](0003-message-broker-rabbitmq.md)
  - [ADR-0004: Vorige beslissing (superseded)](0004-enricher-direct-rabbitmq-superseded.md)
- Gerelateerde diagrammen:
  - [C4 Context](../C4/C4_Context.drawio)
  - [C4 Container](../C4/C4_Container.drawio)
- OpenMRS module developer docs:
  - https://openmrs.atlassian.net/wiki/spaces/docs/pages/25462172/For+Module+Developers
- HL7 FHIR R4 Appointment resource:
  - https://hl7.org/fhir/R4/appointment.html
