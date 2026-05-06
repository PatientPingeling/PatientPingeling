---
status: Accepted
date: 24/04/2026
deciders: [PatientPingeling]
---

# AD: Architectuurkeuze: zelfstandige module vs. ingebouwde module

## Context en Probleembeschrijving

Kiezen we ervoor om de notificatie module zelfstandig of ingebouwd te maken?

Hoe wordt de module gepositioneerd ten opzichte van OpenMRS?

## Beslissingsfactoren

- Driver 1: Seperation of Concern
- Driver 2: Schaalbaarheid
- Driver 3: Integratiegemak
- Driver 4: Onderhoudbaarheid

## Overwogen Opties

1. Zelfstandig
2. Ingebouwd

## Resultaten

We hebben gekozen om de module **Zelfstandig**, dus losstaand van OpenMRS te ontwikkelen.

Onze opdracht beschrijving verplicht ons al om deze keuze te maken. Wel zijn we ook van mening dat een losstaande module met voordelen komt zoals:

- Seperation of Concern: OpenMRS is bedoelt voor het handelen van klinische data, niet voor het versturen van notificaties.
- Schaalbaarheid: De zelfstandige module kan horizontaal opschalen door meerdere instanties te draaien, wat handig is bij een toenemend aantal notificaties of gebruikers.
- Integratiegemak: Een losstaande applicatie kan op een centrale plek verbonden worden met OpenMRS, waardoor het eenvoudiger wordt om wijzigingen of uitbreidingen door te voeren zonder impact op de kern van OpenMRS.
- Onderhoudbaarheid: Als de module niet ingebouwd is, conflicten updates van OpenMRS niet met onze module. De FHIR standaard blijft ook gelijk waar we de informatie voor onze notificaties vandaan halen.

### Gevolgen

- Goed, meer flexibiliteit en integratiegemakt t.o.v. ingebouwde module.
- Goed, betere seperation of concern en onderhoudbaarheid
- Slecht, hogere complexiteit in beheer.

## Meer Informatie

_Geen verdere informatie bij deze ADR_
