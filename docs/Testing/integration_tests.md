# Integratie Testen

## Inleiding
Beschrijf hier kort waarom integratie testen belangrijk zijn voor PatientPingeling en wat het doel is. Anders dan unit testen, testen integratie testen hoe meerdere componenten **samen** werken, zoals de communicatie tussen de enricher-plugin, de Notification API en RabbitMQ.

---

## Automatisch uitvoeren bij build

De integratie testen worden automatisch getriggerd bij elke build van het project via ... (bijv. GitHub Actions, `dotnet test --filter Category=Integration`, etc.). Integratie testen draaien doorgaans na de unit testen in de pipeline.

---

## Overzicht van de integratie testen

### Integratie Test 1 — [Naam van de test]

| Veld | Beschrijving |
|------|-------------|
| **Componenten** | Welke componenten worden samen getest? (bijv. Enricher + Notification API) |
| **Wat wordt getest** | Beschrijf welke integratie of koppeling wordt gecontroleerd |
| **Waarom** | Waarom is deze integratie kritisch voor het systeem? |
| **Randvoorwaarden** | Wat moet er actief/beschikbaar zijn om de test te draaien? (bijv. RabbitMQ, database) |
| **Verwacht resultaat** | Wat is de verwachte uitkomst bij een geslaagde test? |
| **Triggered door** | Automatisch bij build / GitHub Actions / etc. |

#### Screenshot
> Voeg hier een screenshot in van de geslaagde testuitvoering ter validatie.

![Integratie Test 1 resultaat](./screenshots/integratie-test1.png)

---

### Integratie Test 2 — [Naam van de test]

| Veld | Beschrijving |
|------|-------------|
| **Componenten** | |
| **Wat wordt getest** | |
| **Waarom** | |
| **Randvoorwaarden** | |
| **Verwacht resultaat** | |
| **Triggered door** | |

#### Screenshot
> Voeg hier een screenshot in van de geslaagde testuitvoering ter validatie.

![Integratie Test 2 resultaat](./screenshots/integratie-test2.png)

---

Hieronder een afbeelding van het uitvoeren van alle integration tests.

#### Screenshot - Gehele Integration Tests
> Voeg hier een screenshot in van de uitvoering van alle integration tests