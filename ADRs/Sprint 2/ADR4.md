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

- Driver 1: Betrouwbaarheid & Downtime bestendigheid (Opdracht vereist een fallback- en retrymechanische bij downtime providers of OpenMRS)
- Driver 2: Schaalbaarheid naar meerdere OpenMRS instanties
- Driver 3: HL7/FHIR-compliance (queueing en retry mechanische zijn onderdeel van de HL7-standaard)
- Driver 4: Beveiliging (Berichteninhoud moet versleuteld worden, zeker gevoelige patiëntdata)

## Overwogen Opties

1. [Option 1]
2. [Option 2]
3. [Option 3]

## Resultaten

We decided to use **[chosen option]**.

This option was chosen because [main reason]. Other options such as [rejected options] were not selected because [reason they do not fit the context].

### Gevolgen

- Good, because [positive consequence].
- Good, because [positive consequence].
- Bad, because [negative consequence / trade-off].
- Bad, because [negative consequence / risk].

## Meer Informatie

- [Relevant documentation, article, diagram, standard, or source]
- [Related architectural decision]
- [Follow-up decision that still needs to be made]
