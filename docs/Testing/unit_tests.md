# Unit Testen

## Inleiding

Hoe worden de gegevens van een patient verwerkt en worden de afspraken correct in het systeem gezet? Met deze testen controleren we de `AppointmentIngestionService` met het gebruik van **Moq**.

## Automatisch uitvoeren bij build

De unit testen worden automatisch getriggerd bij elke build van het project. Dit gebeurt via `dotnet test` in de CI/CD pipeline en GitHub Actions. Hierdoor worden fouten vroeg opgespoord zonder dat een ontwikkelaar de testen handmatig hoeft uit te voeren.

## Overzicht van de unit testen

### Test 1 — Stop verwerking als patiënt niet bestaat

| Veld                   | Beschrijving                                                                                             |
| ---------------------- | -------------------------------------------------------------------------------------------------------- |
| **Klasse/component**   | `AppointmentIngestionService`                                                                            |
| **Wat wordt getest**   | Of de service direct stopt met de verwerking wanneer een patiënt-ID niet wordt gevonden in de database.  |
| **Waarom**             | Dit voorkomt dat het systeem foutieve afspraken of notificaties gaat inplannen voor onbekende patiënten. |
| **Verwacht resultaat** | De service zoekt via `GetByIdAsync` en merkt op dat de patiënt `null` is                                 |
| **Triggered door**     | Automatisch bij het bouwen (`dotnet build`) of testen (`dotnet test`).                                   |

#### Screenshot

![Test 1 resultaat](./screenshots/test1Unittests.png)

---

### Test 2 — Succesvolle verwerking bij geldige data

| Veld                   | Beschrijving                                                                                                  |
| ---------------------- | ------------------------------------------------------------------------------------------------------------- |
| **Klasse/component**   | `AppointmentIngestionService`                                                                                 |
| **Wat wordt getest**   | Of de service een binnenkomende afspraak succesvol verwerkt wanneer de data klopt en de patiënt bestaat.      |
| **Waarom**             | Dit is het bewijst dat de functionaliteiten van de afspraakverwerking werkt.                                  |
| **Verwacht resultaat** | De service doorloopt het hele proces zonder foutmeldingen en de command-data is na afloop succesvol verwerkt. |
| **Triggered door**     | Automatisch bij het bouwen (`dotnet build`) of testen (`dotnet test`).                                        |

#### Screenshot

![Test 2 resultaat](./screenshots/test2Unittests.png)

---

### Test 3 — Lege invoer controleren

| Veld                   | Beschrijving                                                                                                 |
| ---------------------- | ------------------------------------------------------------------------------------------------------------ |
| **Klasse/component**   | `IngestAppointmentCommand`                                                                                   |
| **Wat wordt getest**   | Of het systeem correct omgaat met een **null** waarde.                                                       |
| **Waarom**             | Dit is een controle om te bewijzen dat het testsysteem stabiel blijft wanneer er geen data wordt meegegeven. |
| **Verwacht resultaat** | De test ziet netjes dat de waarde inderdaad **null** is (`Assert.IsNull`).                                   |
| **Triggered door**     | Automatisch bij het bouwen (`dotnet build`) of testen (`dotnet test`).                                       |

#### Screenshot

![Test 3 resultaat](./screenshots/test3Unittests.png)

---

#### Screenshot - Gehele Unit Tests

![Gehele Unit Tests resultaat](./screenshots/testAllUnittests.png)
