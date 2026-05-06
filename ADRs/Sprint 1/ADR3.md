---
status: [Proposed]
date: 05/05/2026
deciders: [PatientPingeling]
---

# AD: Integratiemethode: hoe koppelt de module aan OpenMRS?

## Context en Probleembeschrijving

Onze module moet afspraakdata uit OpenMRS ontvagen om notificaties te sturen. De vraag is hoe onze module aan deze data gaat komen. 

## Beslissingsfactoren

* Driver 1: Betrouwbaarheid & Downtime bestendigheid (Wat gebeurt er als onze module offline gaat en tijdelijk geen gegevens kan vervangen en verwerken?)
* Driver 2: Schaalbaarheid naar meerdere OpenMRS instanties
* Driver 3: FHIR-aansluiting
* Driver 4: Complexiteit van implementatie

## Overwogen Opties

1. FHIR API Polling: Onze applicatie roept periodiek de OpenMRS FHIR REST API aan om te kijken of er nieuwe gewijzigde afspraken / gegevens zijn. Dit is een simpele mogelijke oplossing, alleen is het wel minder 'real-time' (altijd vertraging d.m.v.) polling interval. Ook kan het erg inefficiënt en kostelijk zijn als we blijven pollen terwijl er niets nieuws is.

2. Event-driven via message-broker (rabbitMQ): OpenMRS heeft al een ingebouwde event module die nieuwe wijzigingen binnen het systeem publiceert. Hier kunnen we dan een message-broker aan koppelen om deze nieuwe data te queuen. Voordelen hiervan zijn dat we alleen de API gebruiken als er ook écht nieuwe data is. Het is ook zeer schaalbaar omdat meerdere OpenMRS instanties naar dezelfde queue kunnen publishen. Wel is het een complexere aanpak vergeleken met pollen en moeten we de event naar het FHIR-formaat mappen. 

3. Database polling: We pollen rechtstreek de OpenMRS database(s) om nieuwe data te vinden. We querying naar specifieke tabellen waar we filteren op gewijzigde of nieuwe rijen. Het voordeel hiervan is dat er geen API-configuratie nodig is. Ook hebben we precieze controle over de data die we ophalen, hoeft niet als FHIR-standaard binnen te komen. Nadelen hierbij is dat bij database-migraties van een in-use OpenMRS systeem onze module kan breken. Geen abstractielaag, geen FHIR-compliance. Het is slecht schaalbaar (slechter dan event driven approach). Het is ook een security-risico om een directe link met een database te hebben, zeker de gevoelige informatie die OpenMRS systemen bevatten. 

## Resultaten

We hebben gekozen voor de **Event-driven via message-broker** aanpak. 

Deze optie kwam met de meeste voordelen, zeker qua schaalbaarheid. Het zou wel wat ingewikkelder kunnen zijn om voor elkaar te krijgen maar de voordelen die het brengt wegen daar zeker tegen op. 

### Gevolgen

* Goed, rabbbit-MQ kan berichten bewaren tijdens een downtime van onze module, zodat er geen informatie verloren gaat. 
* Goed, meerdere instanties van OpenMRS kunnen naar dezelfde queue publishen. --> Horizontale schaalbaarheid
* Goed, aansluiting op onze gekozen technologiestack in ADR2
* Slecht, meeste complexe optie van de drie
* Slecht, De event-module moet nog geconfigureerd worden om te praten met onze message-broker. Ook moeten de events nog gemapped worden naar FHIR

## Meer Informatie

* [OpenMRS Event Module documentatie](https://openmrs.atlassian.net/wiki/spaces/docs/pages/25462172/For+Module+Developers)
* [RabbitMQ durable queues](https://www.rabbitmq.com/docs/queues#durability)
* [HL7 FHIR R4 Appointment resource](https://hl7.org/fhir/R4/appointment.html)