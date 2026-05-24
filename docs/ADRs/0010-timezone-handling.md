# AD: Tijdzone-afhandeling van afspraaktijden

| Eigenschap       | Waarde           |
| ---------------- | ---------------- |
| **Status**       | ✅ Accepted      |
| **Datum**        | 24-05-2026       |
| **Beslissers**   | PatientPingeling |
| **Geraadpleegd** | -                |

## Context en Probleembeschrijving

NFR 13 uit de opdracht eist dat de communicatiemodule diverse tijdzones ondersteunt: alle notificaties en tijdstippen waarop ze worden verstuurd moeten rekening houden met de lokale tijdzone van de betreffende OpenMRS-organisatie. Concreet: een patiënt in Amsterdam moet "om 10:00" lezen voor een afspraak om 10:00 lokale tijd, ongeacht waar onze server draait.

De afspraaktijd doorloopt meerdere systeemgrenzen: OpenMRS → enricher-plugin → HTTP webhook → API → PostgreSQL → RabbitMQ → notification worker → bericht aan patiënt. Op elke grens kan de tijdzone-informatie verloren raken of verkeerd geïnterpreteerd worden. Postgres `timestamptz` slaat bovendien altijd UTC op en preserveert de originele offset niet, dus de wall-clock-betekenis moet elders bewaakt worden.

Hoe handelen we tijdzones consistent af door de hele pipeline heen?

## Beslissingsfactoren

- Driver 1: NFR 13 compliance — patiënt leest correcte lokale tijd
- Driver 2: Eén bron van waarheid voor tenant-tijdzone, geen verspreide aannames
- Driver 3: Plugin-universaliteit — installatie in elke regio zonder code-aanpassing
- Driver 4: Uitbreidbaarheid — nieuwe tenants uit een willekeurige tijdzone aansluiten
- Driver 5: Aansluiting bij Postgres `timestamptz` (UTC-canonical opslag)

## Overwogen Opties

1. **Plugin stuurt UTC, server vertaalt via `Tenant.TimeZone`**: de enricher-plugin formatteert de afspraaktijd in ISO-8601 met `+00:00`. De server slaat UTC op en converteert pas in de notification worker — vlak voor bericht-formattering — naar de tijdzone die op de tenant-row staat.

2. **Plugin stuurt OpenMRS-instantie-lokale tijd, server vertaalt via `Tenant.TimeZone`**: de plugin gebruikt `ZoneId.systemDefault()` of een OpenMRS Global Property. De server doet dezelfde conversie als optie 1, maar de plugin draagt nu ook een tijdzone-aanname.

3. **Plugin stuurt offset, server preserveert offset (geen `Tenant.TimeZone`)**: de plugin levert ISO-8601 met de originele offset. De server bewaart die offset (bv. in een extra kolom of als tekst) en gebruikt 'm direct bij bericht-formattering.

## Resultaten

We hebben gekozen voor **optie 1: plugin stuurt UTC, server vertaalt via `Tenant.TimeZone`**.

**Optie 2 is afgewezen** omdat het twee bronnen van waarheid introduceert: de plugin's idee van "lokale tijd" én de server's `Tenant.TimeZone`. Bij mismatch (plugin draait op een Europese host maar het ziekenhuis bedient patiënten in Azië) leest de patiënt de verkeerde tijd zonder dat het probleem makkelijk te diagnosticeren is.

**Optie 3 is afgewezen** omdat Postgres `timestamptz` de originele offset niet preserveert (alles wordt UTC). Offset bewaren vraagt om een aparte kolom of een tekstuele opslag, wat indexering en datum-rekenwerk omslachtig maakt. Bovendien vertelt een offset niets over de DST-regels van de regio — `+02:00` in januari is gewoon fout voor Amsterdam.

De gekozen aanpak werkt als volgt:

- De plugin formatteert `getStartDateTime()` als `Instant` en serialiseert via `ZoneOffset.UTC` naar ISO-8601 (`2027-06-15T07:30:00+00:00`).
- De API ontvangt deze als `DateTimeOffset`, ingestion roept `.ToUniversalTime()` aan en slaat het UTC-moment op in `Appointment.ScheduledAt` (Postgres `timestamptz`).
- De scheduler bouwt een fat-message met `TenantTimeZone` als IANA-string (bv. `"Europe/Amsterdam"`) opgehaald van de `Tenant` entity.
- De notification worker roept in `NotificationDispatchService.ToTenantLocalTime` de helper aan die het UTC-moment converteert naar de tenant-lokale tijd via `TimeZoneInfo.FindSystemTimeZoneById`, en past deze toe in Email-, SMS- en Push-templates.
- Bij een ontbrekende of onbekende IANA-string valt de helper terug op UTC zonder exceptie — defense in depth.

### Gevolgen

- Goed, omdat de plugin nu universeel is: elke OpenMRS-installatie, ongeacht regio, werkt zonder code-aanpassing.
- Goed, omdat `Tenant.TimeZone` de enige bron van waarheid is voor de tijdzone-betekenis van een afspraak in bericht-context.
- Goed, omdat de opslag in `timestamptz` UTC-canonical blijft, conform Postgres best practice.
- Goed, omdat een onbekende IANA-string niet leidt tot een crash maar tot een UTC-fallback met logbare situatie.
- Slecht, omdat `Tenant.TimeZone` correct gevuld moet zijn per tenant: een onboarding-fout (verkeerde IANA-naam, lege string) leidt tot UTC in patiëntberichten i.p.v. lokale tijd.
- Slecht, omdat de host waarop de containers draaien een actuele IANA tijdzone-database (tzdata) moet hebben; .NET op Linux gebruikt deze native, maar bij verouderde container-images kunnen DST-regels achterlopen.

## Meer Informatie

- IANA Time Zone Database: https://www.iana.org/time-zones
- Postgres `timestamp with time zone` docs: https://www.postgresql.org/docs/current/datatype-datetime.html
- .NET `TimeZoneInfo.FindSystemTimeZoneById`: https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.findsystemtimezonebyid
- Gerelateerde ADRs:
  - [ADR-0006: HTTP webhook contract](0006-enricher-http-webhook.md)
  - [ADR-0009: Scheduler fat-message inhoud](0009-scheduler-message-contents.md)
