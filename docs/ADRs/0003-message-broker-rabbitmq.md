# AD: Queues

| Eigenschap       | Waarde           |
| ---------------- | ---------------- |
| **Status**       | ✅ Accepted      |
| **Datum**        | 06-05-2026       |
| **Beslissers**   | PatientPingeling |
| **Geraadpleegd** | -                |

## Context en Probleembeschrijving

De communicatiemodule moet notificaties versturen naar patiënten via externe messaging providers, op vaste momenten voor een afspraak (24 uur en 1 uur van tevoren). Deze notificaties mogen niet verloren gaan bij tijdelijke downtime van een messaging provider of OpenMRS-instantie.

De vraag is welke message-broker we gebruiken en hoe deze is ingericht.

## Beslissingsfactoren

- Driver 1: Betrouwbaarheid & Downtime bestendigheid (Opdracht vereist een fallback- en retrymechanisme bij downtime providers of OpenMRS)
- Driver 2: Schaalbaarheid naar meerdere OpenMRS instanties (wordt expliciet genoemd door opdrachtbeschrijving)
- Driver 3: HL7/FHIR-compliance (queueing en retry-mechanismen zijn onderdeel van de HL7-standaard)
- Driver 4: Beveiliging (Berichteninhoud moet versleuteld worden, zeker gevoelige patiëntdata)

## Overwogen Opties

(Opties die per broker specifiek kunnen zijn, volgen al onze gekozen broker van optie 1).

1. _Welke Broker / integratiestijl?_
   a. **RabbitMQ (direct)**: Een message broker die berichten asynchroon verwerkt via exchanges en queues. Lichtgewicht, goed gedocumenteerd en breed ondersteund in het .NET ecosysteem. Volledige controle over exchanges, queues, bindings en DLX-configuratie.
   b. **MassTransit op RabbitMQ**: Een .NET abstractielaag over message brokers (RabbitMQ, Azure Service Bus, etc.) die een hogere-level API biedt voor consumers, sagas en retry-policies. Verbergt de onderliggende broker-details achter conventies.
   c. **Apache Kafka**: Een gedistribueerd event-streaming platform dat berichten opslaat als een log. Zeer schaalbaar bij hoge volumes en geschikt voor event-replay.
   d. **Geen broker, maar directe REST-calls**: De meest eenvoudige optie, geen retry of buffering.

2. _Welke queue-topologie?_
   a. **One-way messaging**: Een enkele queue en consumer. De meest simpele vorm van een messaging pattern.
   b. **Competing Consumers (Worker Queues)**: Een enkele queue en één of meerdere consumers. Verhoogt de schaalbaarheid.
   c. **Publish/Subscribe**: Een producer publiceert een bericht dat verspreid wordt naar alle consumers.

3. _Hoe gaan we om met faalgevallen en duplicaten?_
   a. **Dead Letter Exchange (DLX)**: NACK-berichten worden na X pogingen doorgestuurd naar een aparte dead letter queue voor handmatige inspectie.
   b. **Requeue + idempotency-log**: transient errors gaan via `BasicNack(requeue: true)` terug de queue in; permanent errors worden bewust gedropt; duplicate-bezorging wordt afgevangen door een `dispatch_logs` tabel die de laatste verwerkingsstatus per `scheduled_notification_id` bewaart.

## Resultaten

_Gekozen broker_
We hebben gekozen voor **RabbitMQ direct** (optie 1a).

**MassTransit (optie 1b) is afgewezen.** MassTransit is een krachtige abstractie die veel boilerplate wegneemt, maar voor ons project introduceert het onnodige complexiteit. We willen volledige controle over onze RabbitMQ-configuratie (exchanges, DLX-bindings, TTL's) en we willen elke architectuurkeuze kunnen uitleggen. MassTransit verbergt juist die details. Daarnaast is het team nog aan het leren — een extra abstractielaag op een al complexe broker maakt debugging moeilijker. MassTransit blijft een **toekomstige overweging** als het project groeit en de boilerplate zwaarder wordt.

**Apache Kafka (optie 1c) is afgewezen.** We werken niet met extreme volumes en hebben geen behoefte aan event-replay. Kafka brengt meer operationele complexiteit mee dan RabbitMQ zonder meerwaarde voor onze use-case.

**Directe REST-calls (optie 1d) zijn afgewezen.** Geen retry, geen buffering, geen downtime-bestendigheid. Voldoet niet aan de eisen van de opdrachtgever.

_Gekozen queue topologie_
We hebben gekozen voor **Competing Consumers**. Meerdere Worker-instanties kunnen berichten van dezelfde queue consumeren, wat horizontaal schalen mogelijk maakt. Publish/Subscribe is afgewezen omdat het duplicate notificaties zou introduceren.

_Gekozen faal- en duplicaat-afhandeling_
We hebben gekozen voor **optie 3b: requeue + idempotency-log**. DLX (optie 3a) is afgewezen omdat onze betrouwbaarheidsbehoefte al wordt afgedekt door een combinatie van requeue voor transient fouten en de bestaande `dispatch_logs` tabel voor idempotentie en audit:

- Transient errors (provider tijdelijk down, netwerkglitch) → `BasicNack(requeue: true)` — bericht gaat terug de queue in en wordt later opnieuw geprobeerd.
- Permanent errors (geen contactgegevens, ongeldige payload) → `BasicNack(requeue: false)` — bericht wordt bewust gedropt; de bijbehorende `DispatchLog` met `Outcome = ERROR_PERMANENT` blijft in de database als audit-spoor.
- Duplicate-bezorging → de worker bekijkt vóór elke dispatch de nieuwste `dispatch_logs` rij voor dezelfde `scheduled_notification_id`. Bij `SUCCESS` wordt het bericht stil ge-ACK'd zonder opnieuw te verzenden.
- Stale messages (>10 minuten in queue) → worker logt `Outcome = EXPIRED` en ACK't, voorkomt dat oude data alsnog wordt verzonden.

DLX blijft een **toekomstige overweging** wanneer we handmatige inspectie van poison-messages willen of metrics op de DLQ-grootte willen tonen op het Grafana-dashboard.

### Gevolgen

- Goed, omdat RabbitMQ tijdelijk kan bufferen bij downtime van messaging providers, waardoor notificaties niet verloren gaan.
- Goed, omdat competing consumers horizontaal kan schalen door meerdere Worker-instanties te draaien.
- Goed, omdat directe RabbitMQ-configuratie volledige controle geeft over exchanges, bindings en consumer-acknowledgement-gedrag.
- Goed, omdat idempotentie via `dispatch_logs` ervoor zorgt dat dubbele bezorging (bv. na een worker-crash vóór ACK) geen dubbele notificaties oplevert.
- Slecht, omdat permanent gefaalde berichten zonder DLX verloren gaan voor handmatige inspectie — alleen de `DispatchLog` rij blijft over als audit-spoor.
- Toekomstige overweging: MassTransit als de hoeveelheid boilerplate rondom consumers en retry-policies te groot wordt.

## Meer Informatie

- RabbitMQ docs (basis): https://www.rabbitmq.com/docs
- RabbitMQ durable queues (durability/persistence): https://www.rabbitmq.com/docs/queues#durability
- RabbitMQ consumer acknowledgements (ACK/NACK): https://www.rabbitmq.com/docs/confirms
- RabbitMQ Dead Letter Exchanges (DLX): https://www.rabbitmq.com/docs/dlx
- RabbitMQ x-death header (retry/dead-letter metadata): https://www.rabbitmq.com/docs/dlx#x-death
- HL7 FHIR R4 Appointment resource (afspraakdata): https://hl7.org/fhir/R4/appointment.html

- Gerelateerde ADRs:
  - [ADR-0002: Backend C# / .NET](0002-backend-csharp-dotnet.md)
  - [ADR-0006: Integratiemethode HTTP webhook](0006-enricher-http-webhook.md)
- Gerelateerde diagrammen:
  - [C4 Context](../C4/C4_Context.drawio)
  - [C4 Container](../C4/C4_Container.drawio)

- Follow up vragen:
  - Gaan we wel of niet gebruik maken van quorum queues?
