---
status: Proposed
date: 06-05-2026
deciders: PatientPingeling
---

# AD: Queues

## Context en Probleembeschrijving

De communicatiemodule moet notificaties versturen naar patiënten via externe messaging providers, op vaste momenten voor een afspraak (24 uur en 1 uur van tevoren). Deze notificaties mogen niet verloren gaan bij tijdelijke downtime van een messaging provider of OpenMRS-instantie.

De vraag is welke message-broker we gebruiken en hoe deze is ingericht.

## Beslissingsfactoren

* Driver 1: Betrouwbaarheid & Downtime bestendigheid (Opdracht vereist een fallback- en retrymechanische bij downtime providers of OpenMRS)
* Driver 2: Schaalbaarheid naar meerdere OpenMRS instanties (wordt expliciet genoemd door opdrachtbeschrijving)
* Driver 3: HL7/FHIR-compliance (queueing en retry mechanische zijn onderdeel van de HL7-standaard)
* Driver 4: Beveiliging (Berichteninhoud moet versleuteld worden, zeker gevoelige patiëntdata)

## Overwogen Opties
(Opties die per broker specifiek kunnen zijn, volgen al onze gekozen broker van optie 1).

1. *Welke Broker?*
    a. **RabbitMQ**: Een message queuer die bericht asynchroonb verwerkt via exchanges en queues. Ondersteunt ook retry-mechanismen. Lichtgewicht, goed gedocumenteerd en breed ondersteund in het .NET ecosysteem.
    b. **Apache Kafka**: Een gedistribueerd event-streaming platform dat berichten opslaat als een log. Zeer schaalbaar bij hoge volumes (miljoenen berichten per seconde) en geschikt voor meerdere consumers die dezelfde data opnieuw kunnen inlezen.
    c. **Geen broker, maar directe REST-calls tussen OpenMRS en de communicatiemodule**: De meest eenvoudige optie
2. *Welke queue-topologie*
    a. **One-way messaging**: Een enkele queue en consumer. De meest simpele vorm van een messaging pattern. 
    b. **Competing Consumers (Worker Queues)**: Een enkele queue en één of meerdere consumers. Dit verhoogd de schaalbaarheid.
    c. **Publish/Subscribe**: Een producer publiceert een bericht dat hierna wordt verspreid naar alle consumers. 
3. *Welk retry mechanisme?*
    a. **Dead Letter Exchange (DLX)**: 
    b. Geen andere optie gevonden...

## Resultaten

*Gekozen broker*
We hebben gekozen voor **RabbitMQ**. Dit hebben we gedaan omdat het niet gebruik maken van een queue bepaalde eisen van de opdrachtgeveer al faalt. Zo is het slecht schaalbaar en zijn er ook grote kansen op data verliest wat slecht scoort op belissingsfactor 1. De opdracht eist een fallback- en retrymechanischme wat we hiermee niet kunnen bereiken. 
Apache is bij onze use-case overkill. We werken niet met extreme volumes van data en hebben ook geen behoefte aan even-replay. Ook brengt deze oplossing meer complexiteit met zich mee t.o.v. RabbitMQ.
Verder zoals benoemd bij de overwogen opties is RabbitMQ lichtgewicht, goed gedocumenteerd en breed ondersteund in het .NET ecosysteem.

*Gekozen queue topologie*
We hebben gekozen voor **Competing Consumers** omdat dit grotere schaalbaarheid brengt t.o.v. one-way messaging. Als meerdere OpenMRS-systemen gebruik willen maken van ons notificatie systeem, kunnen we nieuwe containers van onze module instantieëren om als competing customer te fungeren. De publish/subrscribe topologie is voor onze use-case niet toepasselijk, omdat het bijvoorbeeld duplicate notificaties zal introduceren. Ook introduceert het onnodige overhead. 

*Gekozen retry mechanisme*
We hebben (uitgezonderd plugins) geen andere retry-mechanisme gevonden voor RabbitMQ naast **DLX**. We zijn van plan DLX te gebruiken plus een 'x-death' header om NACK berichten te retryen en uiteindelijk poison-messages te elimineren. 


### Gevolgen

- Goed, omdat RabbitMQ tijdelijk kan bufferen bij downtime van messaging providers of de communicatiemodule, waardoor notificaties niet verloren gaan.
- Goed, omdat competing consumers horizontaal kan schalen door meerdere instanties van de communicatiemodule te draaien.
- Slecht, omdat DLX/retry-configuratie extra complexiteit en beheer introduceert (bijv. TTL’s, bindings en DLQ monitoring).

## Meer Informatie

- RabbitMQ docs (basis): https://www.rabbitmq.com/docs
- RabbitMQ durable queues (durability/persistence): https://www.rabbitmq.com/docs/queues#durability
- RabbitMQ consumer acknowledgements (ACK/NACK): https://www.rabbitmq.com/docs/confirms
- RabbitMQ Dead Letter Exchanges (DLX): https://www.rabbitmq.com/docs/dlx
- RabbitMQ x-death header (retry/dead-letter metadata): https://www.rabbitmq.com/docs/dlx#x-death
- HL7 FHIR R4 Appointment resource (afspraakdata): https://hl7.org/fhir/R4/appointment.html

- Gerelateerde ADRs:
    - [ADR2 (technologiestack incl. RabbitMQ)](../Sprint%201/ADR2.md)
    - [ADR3 (event-driven integratie via RabbitMQ)](../Sprint%201/ADR3.md)
- Gerelateerde diagrammen:
    - [C4 Context](../../C4/C4_Context.drawio)
    - [C4 Container](../../C4/C4_Container.drawio)

- Follow up vragen:
    - Gaan we wel of niet gebruik maken van quorum queues?