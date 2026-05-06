---
status: Proposed
date: 06-05-2026
deciders: PatientPingeling
---

# AD: Queues

## Context en Probleembeschrijving

De communicatiemodule moet notificaties versturen naar patiënten via externe messaging providers, op vaste momenten voor een afspraak (24 uur en 1 uur van tevoren). Deze notificaties mogen niet verloren gaan bij tijdelijke downtime van een messaging provider of OpenMRS-instantie.

De vraag is hoe de queuing-infrastructuur ingericht moet worden om aan deze eisen te voldoen: welke broker, welke queue-topologie, en welke mechanismen voor retry, fallback en beveiliging.

## Beslissingsfactoren

* Driver 1: Betrouwbaarheid & Downtime bestendigheid (Opdracht vereist een fallback- en retrymechanische bij downtime providers of OpenMRS)
* Driver 2: Schaalbaarheid naar meerdere OpenMRS instanties (wordt expliciet genoemd door opdrachtbeschrijving)
* Driver 3: HL7/FHIR-compliance (queueing en retry mechanische zijn onderdeel van de HL7-standaard)
* Driver 4: Beveiliging (Berichteninhoud moet versleuteld worden, zeker gevoelige patiëntdata)

## Overwogen Opties

*Queuing Structuur*
1. **RabbitMQ**: Een message queuer die bericht asynchroonb verwerkt via exchanges en queues. Ondersteunt ook retry-mechanismen. Lichtgewicht, goed gedocumenteerd en breed ondersteund in het .NET ecosysteem.
2. **Apache Kafka**: Een gedistribueerd event-streaming platform dat berichten opslaat als een log. Zeer schaalbaar bij hoge volumes (miljoenen berichten per seconde) en geschikt voor meerdere consumers die dezelfde data opnieuw kunnen inlezen.
3. **Geen queue, maar directe REST-calls tussen OpenMRS en de communicatiemodule**: De meest eenvoudige optie

*Inrichting Gekozen Structuur (Spoiler: RabbitMQ)*
1. 
2.
3.

## Resultaten

*Queuing Structuur*
We hebben gekozen voor **RabbitMQ**. Dit hebben we gedaan omdat het niet gebruik maken van een queue bepaalde eisen van de opdrachtgeveer al faalt. Zo is het slecht schaalbaar en zijn er ook grote kansen op data verliest wat slecht scoort op belissingsfactor 1. De opdracht eist een fallback- en retrymechanischme wat we hiermee niet kunnen bereiken. 
Apache is bij onze use-case overkill. We werken niet met extreme volumes van data en hebben ook geen behoefte aan even-replay. Ook brengt deze oplossing meer complexiteit met zich mee t.o.v. RabbitMQ.
Verder zoals benoemd bij de overwogen opties is RabbitMQ lichtgewicht, goed gedocumenteerd en breed ondersteund in het .NET ecosysteem.

*Inrichting Gekozen Structuur (Spoiler: RabbitMQ)*
...

### Gevolgen

- Good, because [positive consequence].
- Good, because [positive consequence].
- Bad, because [negative consequence / trade-off].
- Bad, because [negative consequence / risk].

## Meer Informatie

- [Relevant documentation, article, diagram, standard, or source]
- [Related architectural decision]
- [Follow-up decision that still needs to be made]

<!-- NOTES - RABBITMQ QUORUM LIJKT BETROUWBAAR EN GOED VOOR ONS -->
