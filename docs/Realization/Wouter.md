# Realisatie Logboek van Wouter
In dit logboek wordt beschreven welke tools zijn gebruikt tijdens de ontwikkeling van het systeem, waarom deze zijn gebruikt en wordt er gereflecteerd op de toegevoegde waarde & kosten van deze tools.

## Gebruikte ontwikkeltools (IDE's)
| Tool           | Waarom                                                                                                  | Reflectie                                                                                                                    |
| -------------- | ------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| VS Code        | Primaire code-editor voor Java/Maven development van de OpenMRS module                                  | Lichtgewicht en flexibel; met de juiste extensies (Java Extension Pack, XML) goed bruikbaar voor module-ontwikkeling.        |
| Draw.io        | C4 Context- en Containerdiagrammen tekenen voor de architectuurdocumentatie                             | Visueel en makkelijk aan te passen; goed voor het communiceren van de architectuur naar het team en de docent.               |
| DBeaver        | OpenMRS- en Postgres database inspecteren: tabelstructuur bekijken, queries draaien, data verifiëren                 | Onmisbaar om te controleren of de module de juiste tabellen aanmaakt en data correct wegschrijft na deployment.              |
| Postman | REST-endpoints van de OpenMRS REST API handmatig testen | Prettig voor het snel valideren van API-calls zonder telkens de gehele applicatie opnieuw te hoeven deployen. Versnelt het developping process door makkelijk en overzichtelijk te testen.|
| Git| Versiebeheer: branches, commits, merges, pull requests | Volledig benut als team. Goede workflow met feature branches en pull requests voor codereviews.|
| GitHub         | Remote repository, project board, issue tracking en samenwerking| Centraal samenwerkingspunt voor het team; issues hielpen bij het bijhouden van openstaande taken per module-onderdeel. Projects vooral was heel waardevol om de voortgang bij te houden.|
| Markdown       | Alle documentatie schrijven: README, logboeken, ADRs, rapporten, etc.| Simpel en effectief; alles in één formaat houdt de documentatie consistent en versiebeheerbaar naast de code. Hier was ik al wel goed mee bekend.|
| Maven + SDK    | OpenMRS OMOD-bestand bouwen via de OpenMRS Module Archetype en de officiële SDK | Steile leercurve door de specifieke projectstructuur (api/, omod/, pom.xml-hiërarchie), maar `mvn package` geeft een werkend `.omod`-bestand dat direct in OpenMRS te installeren is. Onze plugin vond ik het lastigste om te ontwikkelen vanwege de nieuwe taal en het (voor mij) gigantische OpenMRS systeem.|
| Docker Desktop / Compose |De volledige lokale stack opstarten: RabbitMQ, PostgreSQL én de eigen services (API, Scheduler, Worker)| Enorm handig om met één commande de hele omgeving te kunnen starten. Soms was ik wel vergeten de build stap toe te voegen en had was het even puzzelen met toevoegen van secrets. Dit had ik wel al snel onder de knie. |

## Gebruikte AI tools
| Tool | Waarom | Reflectie |
|------|--------|-----------|
|Claude Chat|Claude beantwoorde vragen die ik had over technologiën, architectuur en code. Ook was het heel effectief om bepaalde ideëen van mij op een rijtje te zetten met plus en minpunten die ik zelf misschien niet had gezien. Verder heb ik het actief gebruikt om snel codevoorbeelden te generen. **Kortom kennisoverdracht, technische vergelijkingen, codevoorbeelden, ideëen toetsen en doordenken.**|Het voornaamste voordeel van het gebruiken van deze AI-tool was de tijdswinst. Als ik vast zit met een probleem is het net als een soort 'tweede' leraar, al moet je dit natuurlijk wel met een korreltje zout nemen; je kunt de tool niet altijd vertrouwen en je moet de informatie dubbel checken + langs je team laten gaan. Vaak heeft het me wel goed geholpen met dingen op een rijtje zetten en mij voorbeelden geven van hoe ik het in de code kan uitwerken. Wel heb ik een aantal keer gehad dat het me de verkeerde kant op heeft gestuurd en dat ik er juist meer tijd door ben verloren. Dit lag ook wel voornamelijk aan mezelf omdat op sommige momenten ik de AI-tool teveel autoriteit gaf en er teveel op vertrouwde. Achteraf gezien heeft deze tool me wel zeker meer geholpen dan dat het weerstand gaf, zeker als ik het goed combineerde met mijn eigen kennis en die van mijn teamgenoten.|
|Gemini Chat|*Het gebruik van deze tool is om dezelfde reden als **Claude Chat**. Claude chat was mijn voornaamste AI-tool, maar soms hielp het om een tweede AI-model dezelfde vragen te stellen omdat de antwoorden soms nog wel konden verschillen.*|←|

## Kosten en Toegevoegde Waarde AI-Tools
| Tool | Kosten | Toegevoegde Waarde(s) |
|------|--------|-----------------------|
|AI (Claude + Gemini)|Controle verlies als AI teveel autoriteit krijgt, mogelijke foutieve code, Onderhoudslast om AI-gegenereerde code te reviewen en corrigeren|Tijdswinst in programmeren (vooral repetitieve taken), Hulpmiddel bij architectuur keuzes, Punt om meer specifieke vragen met betrekking tot dit project te vragen in plaats van generieke vragen die al beantwoord zijn online (bijv. stack-overflow).|

## Voorbeelden van waar AI-tooling is gebruikt
| Input | Ouput |
|-|-|
|Kun je me boilerplate code geven voor het opzetten van een RabbitMQ connection + een publisher in C# .NET? Zie ook deze GitHub [repository](https://github.com/drminnaar/dotnet-rabbitmq). Geef ook mogelijkheden voor de file structuur van hoe ik dit in het project kan implementerne. |De boiler plate code waar ik snippets van hebben kunnen gebruiken om toe te voegen aan het project. Ook heb ik het advies van file structuur gelezen en daarna zelf toegepast.|
|Kun je uitleggen hoe ik in C# een background service opzet die periodiek een database pollt en alleen records pakt die nog op pending staan?|Uitleg en voorbeeldstructuur voor een background service die periodiek pollt en alleen de juiste records verwerkt. Daarna zelf toegepast in de Scheduler.|
|Hoe zorg ik ervoor dat mijn RabbitMQ consumer in .NET een message maar één keer verwerkt en fouten netjes afhandelt?|Uitleg over idempotentie, retry-gedrag en foutafhandeling, wat ik daarna heb gebruikt bij het verwerken van queue messages. Zo kregen we als team ook het inzicht dat we een scheduled notification ID kunnen gebruiken als idempotency key.|

## Verbeterpunten
Na het reflecteren op de gebruikte tools, zijn een aantal verbeterpunten naar voren gekomen:
- Bij het gebruik van AI is het belangrijk om de output als concept te gebruiken. Review de output altijd zelf en implementeer het niet klakkeloos. Soms had ik de AI-tools teveel autoriteit gegeven wat me dan weer meer tijd kostte om het op te lossen / te begrijpen. 
- Op bepaalde plekken waar ik AI als tool heb ingezet, heb ik het niet altijd expliciet vermeld bij de Git-logging (bijvoorbeeld CO-author claude + waar). Het is beter om dit wel te doen om als team samen de AI-code nog eens te reviewen. 
- **BUITEN TOOLING:** *Tijdens het project verliep de samenwerking ook niet altijd naar behoren. Dit had ik als individu ook eerder bij de docenten kunnen melden om dit aan te kaarten. Nu heb dat telaat gedaan.*

## Bijdrage aan Project (Commits)
*Navigeer ook naar https://github.com/orgs/PatientPingeling/projects/2/views/1 en https://github.com/PatientPingeling/PatientPingeling/commits/main/ voor de op GitHub bijgehouden Project en de gehele commit geschiedenis*

| Issue/Onderdeel | Beschrijving | Datum |
|--------|-------------|-------|
| [OpenMRS Enricher Plugin](https://github.com/PatientPingeling/PatientPingeling/commit/03d8420d13f26abe5684f6846275d3429711aec7)|Deze plugin + de webhook maakt de verbinding tussen OpenMRS en ons notificatiesysteem. De enricher plugin luistert naar de OpenMRS event-module (appointments only) en verrijkt deze (event stuurt alleen GUID) voor verzenden naar de webhook.|13/05/26|
| [Scheduler Background Service](https://github.com/PatientPingeling/PatientPingeling/commit/9e3bbeecc9bb532ebd296f4d7f4c3b9388f069df)|Deze background service pollt onze database om te kijken of er notificaties klaar staan om verzonden te worden. Deze komen dan terecht op de RabbitMQ queue.| 20/05/26 |
| [RabbitMQ Producer Connection](https://github.com/PatientPingeling/PatientPingeling/commit/9e3bbeecc9bb532ebd296f4d7f4c3b9388f069df#diff-f533505f6fe7f60f5f7c127677d519044b0039c3a82969ca3c059b378d15491f)|Verbinding tussen RabbitMQ en de Scheduler opgezet waar de scheduler een producer is.|20/05/26|
| [OpenMRS Systeem ](https://github.com/PatientPingeling/PatientPingeling/commit/42f9b67c5e9c88c6af9ede1fb031bd38d6a9062f#diff-e45e45baeda1c1e73482975a664062aa56f20c03dd9d64a827aba57775bed0d3)|Global props binnen het openMRS systeem toegevoegd bij opstarten via docker compose. Deze properties worden gebruikt om als OpenMRS user een door ons meegegeven API-Key en tenant-ID in te voeren.|14/05/26|
| [FMEA Tabel Docs](https://github.com/PatientPingeling/PatientPingeling/commit/2e41890f2f41a8905b48dc6a9bfb86f1f5088efb)|FMEA tabel gemaakt van ons systeem. Grootste mogelijke problemen worden hier in vermeld en hun mogelijke oplossingen.|22/05/26|
| [Technische Handleiding Plugin](https://github.com/PatientPingeling/PatientPingeling/commit/a7ff2d8d42e93053e3bafa3797f6ded1f9b20caf)|Een instructie geschreven voor hoe een OpenMRS-gebruiker succesvol onze plugin kan installeren.|22/05/26|
| [C4 model](https://github.com/PatientPingeling/PatientPingeling/commit/bb704bc0fcf4dc533393f23d1f21eb967dd0e3cb)|Wijzigingen aan het C4 container en component diagram gemaakt na herzien van architectuur gedurende het project.|06/05/26  - 25/05/26|
| [Project Structuur](https://github.com/PatientPingeling/PatientPingeling)|Folder structuur van het project ingericht|06/05/26  - 25/05/26|
| [Architectuur Beslissingen - ADR's](https://github.com/PatientPingeling/PatientPingeling/commit/857be76337d2a3c874b5ea8dfd244e49451551d8)|Meegedacht aan structuur en meerdere ADR's geschreven binnen onze documentatie. Deze stonden ook natuurlijk open voor verandering gedurende project|06/05/26  - 25/05/26|
| [Domeinentiteiten](https://github.com/PatientPingeling/PatientPingeling/commit/9e3bbeecc9bb532ebd296f4d7f4c3b9388f069df)|Modellen en Interfaces gemaakt om bepaalde toevoegen aan ons systeem te faciliteren + overzichtelijk en onderhoudbaar maken.|20/05/26|
| [Docker Compose File](https://github.com/PatientPingeling/PatientPingeling/commit/42f9b67c5e9c88c6af9ede1fb031bd38d6a9062f#diff-e45e45baeda1c1e73482975a664062aa56f20c03dd9d64a827aba57775bed0d3)|Toevoegingen aan de docker compose file voor het openMRS systeem (global properties en een .omod installer)|14/05/26|
| [Docs ERD](https://github.com/PatientPingeling/PatientPingeling/commit/ec4281bc8dec232f49230630e0aae4a271bd91bc)|Update van ons ERD model nadat we een dispatchlog tabel hebben toegevoegd.|23/05/26|
| [Docs Chaos Testing](https://github.com/PatientPingeling/PatientPingeling/commit/189e4acde8cc9e5ca055ffbba813472f8906080f)|Handmatige chaos tests uitgevoerd door verschillende components offline te halen terwijl er data binnenkomt.|25/05/26|
*Sommige functionaliteiten konden niet allemaal gelinkt worden aan dezeflde merge/commit message. Ook is er natuurlijk werk van mij dat onder andere namen op github staat, bijvoorbeeld na het overleggen van de structuur met het team wordt het C4 model gecreëerd/aangepast*