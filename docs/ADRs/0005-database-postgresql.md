# AD: Database voor de communicatiemodule

| Eigenschap       | Waarde             |
|------------------|--------------------|
| **Status**       | ✅ Accepted        |
| **Datum**        | 24-04-2026         |
| **Beslissers**   | PatientPingeling   |
| **Geraadpleegd** | -                  |

## Context en Probleembeschrijving

De communicatiemodule heeft een eigen database nodig om afspraakprojecties, reminder-statussen en notificatiepogingen lokaal op te slaan. Hierdoor kan de module zelfstandig bepalen wanneer een reminder verstuurd moet worden, zonder de OpenMRS-database rechtstreeks te benaderen.

Welke databasetechnologie wordt gebruikt voor de communicatiemodule?

## Beslissingsfactoren

- Betrouwbare relationele opslag
- Goede ondersteuning voor event- en statusdata
- Eenvoudig lokaal te draaien in Docker Compose
- Geen extra licentiekosten of platformkeuzes

## Overwogen Opties

1. **PostgreSQL**
2. **MySQL**
3. **MS SQL Server**

## Resultaten

We hebben gekozen voor **PostgreSQL**.

We gebruiken een eigen PostgreSQL-database binnen de communicatiemodule. Dit is nadrukkelijk niet de OpenMRS-database. PostgreSQL sluit aan bij onze behoefte aan betrouwbare relationele opslag, sterke query-mogelijkheden en flexibele opslag van event- en statusdata.

**MySQL** is niet gekozen omdat PostgreSQL beter aansluit bij onze behoefte aan betrouwbare relationele opslag en flexibele opslag van event-/statusdata.

**MS SQL Server** is niet gekozen omdat PostgreSQL lichter en eenvoudiger lokaal te draaien is in onze Docker Compose omgeving en geen extra licentie- of platformkeuzes introduceert.

### Gevolgen

- Goed, omdat PostgreSQL betrouwbare opslag biedt voor afspraakprojecties, reminder-statussen en notificatiepogingen.
- Goed, omdat de module zelfstandig kan bepalen welke notificaties nog verstuurd moeten worden na downtime.
- Goed, omdat PostgreSQL eenvoudig te integreren is in Docker Compose zonder licentiekosten.
- Slecht, omdat een eigen database extra beheer, migraties en dataconsistentie vraagt.

## Meer Informatie

- [PostgreSQL documentatie](https://www.postgresql.org/docs/)
