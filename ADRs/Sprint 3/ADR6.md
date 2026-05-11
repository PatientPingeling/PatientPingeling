---
status: Proposed
date: 24-04-2026
deciders: PatientPingeling
---

# AD: Monitoring en dashboarding

## Context en Probleembeschrijving

De communicatiemodule moet real-time inzicht bieden in verstuurde en mislukte berichten, throughput, actieve foutmeldingen en circuit breaker-activiteit. OpenMRS-beheerders moeten deze informatie kunnen inzien via een operationeel dashboard.

Welke tooling wordt gebruikt voor monitoring en dashboarding van de communicatiemodule?

## Beslissingsfactoren

- Real-time operationeel inzicht (status, throughput, foutactiviteit)
- Aansluiting bij sprintdoel voor observability
- Configuratie-eenvoud in Docker Compose omgeving
- Geen onnodige ontwikkeltijd aan dashboardbouw

## Overwogen Opties

1. **Grafana**
2. **Kibana / OpenSearch Dashboards**
3. **Eigen dashboard bouwen**

## Resultaten

We hebben gekozen voor **Grafana**.

Grafana sluit aan op het sprintdoel waarin OpenMRS-beheerders real-time inzicht moeten krijgen in verstuurde en mislukte berichten, throughput, actieve foutmeldingen en circuit breaker-activiteit. De communicatiemodule publiceert hiervoor metrics en statusinformatie die in Grafana zichtbaar gemaakt worden.

**Kibana / OpenSearch Dashboards** is niet gekozen omdat die optie vooral sterk is voor loganalyse, terwijl onze sprint expliciet vraagt om een real-time operationeel dashboard met status, throughput en foutactiviteit.

**Een eigen dashboard bouwen** is niet gekozen omdat dit meer ontwikkeltijd kost en minder snel productiewaardige monitoring oplevert dan Grafana.

### Gevolgen

- Goed, omdat Grafana real-time inzicht geeft in betrouwbaarheid, throughput en foutscenario's.
- Goed, omdat Grafana snel te integreren is in de bestaande Docker Compose omgeving.
- Slecht, omdat Grafana en metrics extra configuratie en beheer toevoegen aan de Docker-omgeving.

## Meer Informatie

- [Grafana documentatie](https://grafana.com/docs/grafana/latest/)
