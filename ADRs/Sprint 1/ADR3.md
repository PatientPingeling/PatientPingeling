---
status: Accepted
date: 05-05-2026
deciders: PatientPingeling
---

# AD: Integratiemethode: hoe koppelt de module aan OpenMRS?

## Context en Probleembeschrijving

De communicatiemodule is een zelfstandig systeem dat afspraakgegevens uit OpenMRS nodig heeft om notificaties te kunnen versturen. Omdat de module bewust losstaat van OpenMRS, moet er een integratiemethode gekozen worden waarmee afspraakdata betrouwbaar en schaalbaar uitgewisseld kan worden.

Hoe ontvangt de communicatiemodule afspraakdata vanuit OpenMRS?

## Beslissingsfactoren

* Driver 1: Betrouwbaarheid & Downtime bestendigheid (Wat gebeurt er als onze module offline gaat en tijdelijk geen gegevens kan vervangen en verwerken?)
* Driver 2: Schaalbaarheid naar meerdere OpenMRS instanties
* Driver 3: FHIR-aansluiting
* Driver 4: Complexiteit van implementatie

## Overwogen Opties

1. **FHIR API Polling**: De communicatiemodule roept periodiek de OpenMRS FHIR REST API aan om te controleren of er nieuwe of gewijzigde afspraken zijn. Dit is een eenvoudige aanpak, maar introduceert altijd een vertraging gelijk aan het polling-interval. Bovendien is het inefficiënt wanneer er geen nieuwe data is, omdat er dan toch netwerk- en verwerkingslast ontstaat.

2. **Event-driven via berichtenwachtrij (RabbitMQ)**: OpenMRS beschikt over een ingebouwde event-module die wijzigingen binnen het systeem publiceert. Door hierop een berichtenwachtrij te koppelen, ontvangt de communicatiemodule alleen berichten wanneer er daadwerkelijk nieuwe of gewijzigde data is. Meerdere OpenMRS-instanties kunnen naar dezelfde wachtrij publiceren, wat horizontale schaalbaarheid biedt. De aanpak is complexer dan polling en vereist een mapping van OpenMRS-events naar het FHIR-formaat.

3. **Database polling**: De communicatiemodule leest rechtstreeks uit de OpenMRS-database om nieuwe of gewijzigde rijen op te sporen. Dit vereist geen API-configuratie en geeft directe controle over de opgehaalde data. Nadelen zijn echter aanzienlijk: directe databasekoppelingen zijn een beveiligingsrisico gezien de gevoelige medische gegevens in OpenMRS, bieden geen FHIR-compliance, en breken bij database-migraties van een actief OpenMRS-systeem.

## Resultaten

We hebben gekozen voor de **event-driven aanpak via een berichtenwachtrij (RabbitMQ)**.

Deze optie sluit het best aan bij de eisen rondom betrouwbaarheid, schaalbaarheid en FHIR-compliance. De hogere implementatiecomplexiteit weegt op tegen de structurele voordelen, met name de mogelijkheid om berichten te bewaren tijdens downtime en om meerdere OpenMRS-instanties te ondersteunen zonder aanpassingen aan de module zelf.

### Gevolgen

* Goed, omdat RabbitMQ berichten bewaart tijdens een tijdelijke downtime van de communicatiemodule, zodat er geen afspraakgegevens verloren gaan.
* Goed, omdat meerdere OpenMRS-instanties naar dezelfde wachtrij kunnen publiceren, wat horizontale schaalbaarheid ondersteunt.
* Goed, omdat de gekozen aanpak aansluit op de technologiestack uit ADR2 (RabbitMQ als berichtenwachtrij).
* Goed, omdat de module niet rechtstreeks de OpenMRS-database benadert, wat het beveiligingsrisico beperkt.
* Slecht, omdat de event-driven aanpak de meest complexe optie is van de drie en meer initiële configuratie vereist.
* Slecht, omdat de integratie een mapping vereist van OpenMRS-events naar het FHIR-formaat, wat extra implementatiewerk betekent.

## Meer Informatie

* [OpenMRS Event Module documentatie](https://openmrs.atlassian.net/wiki/spaces/docs/pages/25462172/For+Module+Developers)
* [RabbitMQ durable queues](https://www.rabbitmq.com/docs/queues#durability)
* [HL7 FHIR R4 Appointment resource](https://hl7.org/fhir/R4/appointment.html)