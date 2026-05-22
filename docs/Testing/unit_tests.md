# Unit Testen

## Inleiding

De unit testen controleren de businesslogica in de **Application-laag** volledig geïsoleerd van externe systemen (database, RabbitMQ, HTTP-providers). Alle externe afhankelijkheden worden vervangen door **Moq** mocks. Dit maakt de tests snel, deterministisch en zonder infrastructuur uitvoerbaar.

De testen dekken vier services:

| Service | Verantwoordelijkheid |
| ------- | -------------------- |
| `AppointmentIngestionService` | Verwerkt CREATED / UPDATED / CANCELLED webhooks |
| `TenantService` | Valideert API-keys van tenants |
| `NotificationDispatchService` | Stuurt notificaties via de juiste provider en format |
| `NotificationMessageFactory` | Verrijkt scheduled notifications met patient- en tenant-data |

Naast de services worden ook de `Result<T>` en `Error` domeintypen getest.

## Automatisch uitvoeren

De unit testen worden automatisch getriggerd via de CI/CD pipeline (GitHub Actions) bij elke push. Lokaal uitvoeren:

```bash
dotnet test tests/NotificationService.UnitTests
```

## Code coverage

| Meting | Waarde |
| ------ | ------ |
| Line coverage (Application + Domain) | ~91% |
| Branch coverage (Application + Domain) | ~89% |
| Totaal aantal tests | **53** |

Coverage wordt gemeten met `coverlet.collector`:

```bash
dotnet test tests/NotificationService.UnitTests --collect:"XPlat Code Coverage"
```

---

## AppointmentIngestionService (22 tests)

### Guard clauses

| Veld | Beschrijving |
| ---- | ------------ |
| **Wat wordt getest** | Null-command, lege ExternalId's, onbekende action |
| **Waarom** | Foutieve input moet vroeg worden afgewezen vóór databasetoegang |
| **Verwacht resultaat** | `Result.IsFailure` met `ErrorType.Validation` |

### CREATED — nieuw appointment

| Veld | Beschrijving |
| ---- | ------------ |
| **Wat wordt getest** | Nieuwe patiënt aanmaken + appointment opslaan inclusief scheduled notifications |
| **Waarom** | Het kernpad: webhook binnenkomt → alles atomisch in de database |
| **Verwacht resultaat** | `Result.IsSuccess`, `AddAsync` aangeroepen voor patiënt én appointment |

### CREATED — duplicate

| Veld | Beschrijving |
| ---- | ------------ |
| **Wat wordt getest** | Appointment met zelfde ExternalId bestaat al |
| **Waarom** | Idempotency: dezelfde webhook twee keer sturen mag niet leiden tot dubbele data |
| **Verwacht resultaat** | `Result.IsFailure` met `ErrorType.Duplicate` |

### CREATED — notificatieplanning (4 scenarios)

| Veld | Beschrijving |
| ---- | ------------ |
| **Wat wordt getest** | Aantal scheduled notifications op basis van hoe ver de afspraak in de toekomst ligt |
| **Waarom** | De planningslogica heeft 4 takken: >24u, 1-24u (dicht), 1-24u (ver), <1u |
| **Verwacht resultaat** | 2 notifications (>24u of 1-24u ver), 1 notification (1-24u dicht of <1u) |

### CANCELLED

| Veld | Beschrijving |
| ---- | ------------ |
| **Wat wordt getest** | Appointment annuleren: `IsCancelled = true`, CANCELLED dispatch logs schrijven |
| **Waarom** | Geannuleerde afspraken mogen niet meer worden verstuurd door de Scheduler |
| **Verwacht resultaat** | `Result.IsSuccess`, `UpdateAsync` aangeroepen, 1 CANCELLED log per pending notification |

### UPDATED

| Veld | Beschrijving |
| ---- | ------------ |
| **Wat wordt getest** | Upsert bij onbekend appointment, hertijdig plannen bij tijdwijziging, patiëntupdate |
| **Waarom** | UPDATED heeft meerdere paden afhankelijk van wat er verandert |
| **Verwacht resultaat** | Afhankelijk van scenario: add of update, al dan niet nieuwe notifications |

---

## TenantService (7 tests)

| Test | Verwacht resultaat |
| ---- | ------------------ |
| Lege `tenantId` | `ErrorType.Validation` |
| Lege of whitespace `apiKey` | `ErrorType.Validation` |
| Database gooit exception | `ErrorType.Failure` |
| Tenant niet gevonden | `ErrorType.NotFound` |
| API-key komt niet overeen | `ErrorType.Unauthorized` |
| Correcte API-key | `Result.IsSuccess` |

---

## NotificationDispatchService (7 tests)

| Test | Verwacht resultaat |
| ---- | ------------------ |
| Geen enkel format past (geen email, geen telefoon) | `ErrorType.Validation` |
| Decryptie van credentials gooit exception | `ErrorType.Failure` |
| Provider `SendAsync` gooit exception | `ErrorType.Failure` |
| Email-format succesvol | `Result.IsSuccess` + external message ID |
| SMS-format → telefoon als ontvanger | `Result.IsSuccess`, recipient = telefoonnummer |
| Push-format → telefoon als ontvanger | `Result.IsSuccess` |

---

## NotificationMessageFactory (8 tests)

| Test | Verwacht resultaat |
| ---- | ------------------ |
| Lege input | Lege array terug |
| Alle notifications al in queue (INQUEUE status) | Lege array, geen DB-call |
| Notification zonder log → eligible | `GetByIdWithDetailsAsync` wordt aangeroepen |
| Notification met NEW-status → eligible | `GetByIdWithDetailsAsync` wordt aangeroepen |
| Details zijn `null` | Notification overgeslagen |
| Tenant heeft geen credentials | Notification overgeslagen |
| Geldige notification met credentials | 1 `NotificationMessage` terug |
| Mix van eligible en ineligible | Alleen eligible worden opgehaald |

---

## Result & Error (9 tests)

| Test | Verwacht resultaat |
| ---- | ------------------ |
| `Result.Success()` | `IsSuccess = true`, `IsFailure = false` |
| `Result.Failure(error)` | `IsFailure = true`, error correct ingesteld |
| `Result` aanmaken met success=true én non-None error | `InvalidOperationException` |
| `Result` aanmaken met success=false én `Error.None` | `InvalidOperationException` |
| `Result<T>.Value` bij success | Waarde correct teruggegeven |
| `Result<T>.Value` bij failure | `InvalidOperationException` |
| `Error.None` | Code en message zijn leeg |
| `Error` zonder expliciet type | Standaard `ErrorType.Failure` |

---

#### Screenshot — Testresultaten

![Unit test resultaten](./Screenshots/testAllUnittests.png)
