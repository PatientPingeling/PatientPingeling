# AD: Backend programmeertaal en framework

| Eigenschap       | Waarde             |
|------------------|--------------------|
| **Status**       | ✅ Accepted        |
| **Datum**        | 24-04-2026         |
| **Beslissers**   | PatientPingeling   |
| **Geraadpleegd** | -                  |

## Context en Probleembeschrijving

Bij de bouw van de communicatiemodule moet een keuze gemaakt worden voor de programmeertaal en het backend framework. Deze keuze heeft directe invloed op de onderhoudbaarheid, schaalbaarheid en de aansluiting bij de kennis van het team.

Welke programmeertaal en welk framework worden gebruikt voor de communicatiemodule?

## Beslissingsfactoren

- Aansluiten bij teamkennis
- Ondersteuning voor Web API's en background workers
- Ecosysteem voor integraties (RabbitMQ, PostgreSQL, observability tooling)
- Onderhoudbaarheid en moderne taalfeatures

## Overwogen Opties

1. **C# / .NET**
2. **Node.js / JavaScript**
3. **Java / Spring Boot**

## Resultaten

We hebben gekozen voor **C# / .NET**.

C# sluit sterk aan bij de kennis van het team en heeft veel overeenkomsten met Java. .NET biedt goede ondersteuning voor Web API's, background workers, dependency injection, async/await en integraties met RabbitMQ, PostgreSQL en observability tooling.

**Node.js / JavaScript** is niet gekozen omdat het team voor deze module sterker is in C# en omdat de worker/scheduler-structuur van .NET beter past bij langdurige background processing.

**Java / Spring Boot** is niet gekozen omdat OpenMRS al Java gebruikt, maar de communicatiemodule bewust losstaat van OpenMRS en het team meer snelheid verwacht met C# / .NET.

De module draait als een combinatie van een **.NET Web API** en een **.NET background service**. De Web API ondersteunt voorbeeldrequests en beheer-/dashboardscenario's. De background service verwerkt berichten uit RabbitMQ en voert periodiek geplande taken uit, zoals het controleren van afspraken waarvoor 24 uur of 1 uur van tevoren een notificatie nodig is.

### Gevolgen

- Goed, omdat C# / .NET goed aansluit bij de kennis van het team en een modern ecosysteem heeft.
- Goed, omdat .NET uitstekende ondersteuning biedt voor zowel Web API's als long-running background services in één project.
- Slecht, omdat developers met verschillende stacks moeten werken. Er worden 2 verschillende talen en frameworks gebruikt bij het werken aan de notificatie module met OpenMRS en C#.

## Meer Informatie

- [.NET Web API documentatie](https://learn.microsoft.com/en-us/aspnet/core/web-api/)
- [.NET Worker Services documentatie](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)
