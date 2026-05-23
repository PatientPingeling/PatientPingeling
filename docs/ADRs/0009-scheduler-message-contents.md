# AD: Scheduler Message Inhoud

| Eigenschap       | Waarde                                                       |
| ---------------- | ------------------------------------------------------------ |
| **Status**       | ✅ Accepted                                                  |
| **Datum**        | 23-05-2026                                                   |
| **Beslissers**   | PatientPingeling                                             |
| **Geraadpleegd** | -                                                            |

## Context en Probleembeschrijving

Om notificaties uiteindelijk naar de providers te krijgen komen ze door een RabbitMQ queue aan op de notification worker. De scheduler levert de berichten aan om op de queue te zetten. Wel hebben we hier de mogelijkheid om de scheduler **thin** of **fat** messages op de queue te laten zetten. 

Welk van deze opties is het beste voor ons project?

## Beslissingsfactoren

- Ahankelijkheid, minder afhankelijkheid tussen systeem componenten
- Schaalbaarheid van het systeem
- Actualiteit, is de data altijd actueel
- Onderhoudbaarheid en systeem evolutie, werkt het goed bij eventuele toekomstige uitbreidingen/veranderingen?
- Complexiteit van het systeem

## Overwogen Opties

1. De scheduler een **thin** message naar de queue laten sturen met alleen het scheduled notification ID. 
2. De scheduler een **fat** message naar de queue laten sturen dat alle informatie bevat voor het sturen van een notificatie. 

## Resultaten

We hebben gekozen voor **optie 1: fat message**.

We hebben voor deze optie gekozen omdat het ervoor zorgt dat de notificatie worker dan niet op een aparte database connectie alle notificatie informatie moet ophalen. Dit is dus beter voor de schaalbaarheid van het systeem. Wel kan dit de actualiteit beinvloeden als een fat message enige tijd vast zit in de queue en in de tussen tijd de informatie van de scheduled notification verandert of gecanceled wordt. Bij een thin messages zou deze informatie pas gevuld worden net voor het moment van verzending. Dit probleem kunnen we wel oplossen door een sentAt key-value mee te geven in de JSON message en dan te checken in de notification worker of deze tijd niet x aantal minuten heeft overschreden. In dat geval laten we hem door de scheduler opnieuw op de queue zetten.

### Gevolgen

- Goed, schaalbaarheid van het systeem door het ontlasten van de database.
- Goed, complexiteit van het systeem gaat omlaag omdat er op één plek nu gelijk alle notification informatie wordt gevuld i.p.v. dat dit opgesplitst is. 
- Slecht, omdat de actualiteit van de data niet gegarandeerd is (workaround via sentAt key-value, introduceert wel extra logica)

## Meer Informatie
*geen*