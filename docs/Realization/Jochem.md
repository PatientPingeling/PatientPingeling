# Realisatie Logboek van Jochem

In dit logboek wordt beschreven welke tools zijn gebruikt tijdens de ontwikkeling van het systeem, waarom deze zijn gebruikt en wordt er gereflecteerd op de toegevoegde waarde & kosten van deze tools.

## Gebruikte ontwikkeltools (IDE's)

| Tool                     | Waarom                                         | Reflectie                                                                                                                                                                    |
| ------------------------ | ---------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Visual Studio            | Code editor                                    | C# editor die kan werken met .sln files                                                                                                                                      |
| Visual Studio Code       | Postgress extensie                             | Lichtgewicht editor gebruikt om de database mee te benaderen via de Postgress extension en handig bij het werken met markdown files of projecten zonder sollution of C# code |
| Git                      | Versiebeheer                                   | Voor het creeren van branches, bekijken of pushen van nieuwe commits en het revieuwen van code                                                                               |
| Github                   | Github projects                                | Sprint taken en requirements inzien en de algemene project management                                                                                                        |
| Github desktop           | Merge, pull en commit changes met versiebeheer | Makkelijk mergen pullen en comitten met een simpele UI voor Github                                                                                                           |
| Docker desktop / compose | Container management                           | De hele stack opstarten met een commando, ook handig voor integration testing met tijdelijke containers                                                                      |
| Markdown                 | documentatie                                   | Schrijven van documentatie in een formaat en makkelijk te gebruiken met versiebeheer                                                                                         |

## Gebruikte AI tools

| Tool   | Waarom                                                                                                                                            | Reflectie                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Gemini | Gemini hielp met het opzetten van testcases en vragen over technologieen binnen het OpenMRS systeem om de architectuur beter te kunnen begrijpen. | Ik heb met het gebruik van Gemini veel tijd kunnen winnen wat betreft het leren over de architectuur van openMRS. Met eventuele problemen is het handig om Gemini te gebruiken als peer en fouten of flaws zelf te ontdekken. Het is ook handig om problemen op te sporen over dingen die ik zelf over het hoofd had gezien en hier van te leren doordat AI deze problemen kon uitleggen. Wat betreft de testcases en het opzetten van test functies binnen MSTest heeft het mij niet geholpen, het heeft het zelfs langer laten duren door het geven van verouderde informatie. Gelukkig heb ik voor alles goede feedback ontvangen binnen het team en is het gebruik van AI minimaal gebleven. |
|        |                                                                                                                                                   |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |

## Kosten en Toegevoegde Waarde AI-Tools

| Tool   | Kosten                                                                                                 | Toegevoegde Waarde(s)                                                                                                        |
| ------ | ------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| Gemini | Tijd verloren door onjuiste of verouderde informatie en het oplossen van de daardoor ontstaande fouten | Tijd gewonnen door duidelijke uitleg en het begrijpen van het verouderde openMRS systeem of de architectuur van ons project. |

## Voorbeelden AI-tooling gebruik

- Effectieve testcases opstellen voor MSTest met bijbehorende uitleg over hoe dit helpt.
- Begrijpen van het OpenMRS systeem en hoe onze systemen met elkaar communiceren.
- Peer feedback met heldere uileg en verbeterpunten.

## Verbeterpunten

Na het reflecteren op de gebruikte tools, zijn een aantal verbeterpunten naar voren gekomen:

- AI is een tool en moet zo ook gebruikt worden. AI kan goed uitleggen en goed leren op een manier die jij als student fijn vind. Het probleem met het gebruik van AI is dat je meestal veel meer tijd kwijt bent aan het vinden wat juist is of het oplossen van fouten die jij als nietswetende student over het hoofd hebt gezien. Fouten kunnen met het gebruik van AI zelfs je hele project chaotisch en nog ingewikkelder maken terwijl de documentatie een veel heldere uitleg kan bieden. Het is belangrijk de antwoorden altijd dubbel na te gaan.
- Tijdens het project en de lessen had ik veel zelfstudie gedaan. Ik had ergens gewild meer mee te helpen aan het project en had achteraf meer met het team bezig moeten zijn. Communicatie is belangrijk en ik merk daar vooral in een team moeite mee te hebben wat in dit geval kan leiden tot een oneven rolverdeling.
- Daily standups en algemene meetings zijn erg belangrijk. Ik merk dat als je geen dagelijks moment hebt om elkaar te spreken je lang vast kan zitten aan een specifiek probleem, vooral als het project waar je in werkt niet door jou geconfigureerd of opgezet is. Het is belangrijk dit soort contact momenten duidelijk vooraf te bespreken, zodat iedereen aanwezig kan zijn voor vragen.

## Bijdrage aan Project (Commits)

_Navigeer ook naar https://github.com/orgs/PatientPingeling/projects/2/views/1 en https://github.com/PatientPingeling/PatientPingeling/commits/main/ voor de op GitHub bijgehouden Project en de gehele commit geschiedenis_

| Issue/Onderdeel                                                                                                              | Beschrijving                                             | Datum      |
| ---------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------- | ---------- |
| [ADR 2](https://github.com/PatientPingeling/PatientPingeling/commit/83fcdc83ceb4982f3212788bd53349e8b35e530c)                | Opzetten van ADR 2                                       | 24-04-2026 |
| [Updated README.md](https://github.com/PatientPingeling/PatientPingeling/commit/93a3364111d83a9ecc5acda25b6b169fc7b5ed5e)    | Veranderde .env sectie in de README                      | 21-05-2026 |
| [Tests geconfigureert](https://github.com/PatientPingeling/PatientPingeling/commit/3ba52b02705b70deef104166c3fe68052ee460ee) | Opzetten van tests in het project met gebruik van MSTest | 21-05-2026 |
| [Test documentatie](https://github.com/PatientPingeling/PatientPingeling/commit/7ca332506a3cc2e5c0a04afdba1ccc30063119f7)    | Test documentatie                                        | 21-05-2026 |
| Scheduler background service (Niet gebruikt)                                                                                 | Background polling service voor de scheduler             | 15-05-2026 |
