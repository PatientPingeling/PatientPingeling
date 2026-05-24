# Realisatie Logboek van Youp
In dit logboek wordt beschreven welke tools zijn gebruikt tijdens de ontwikkeling van het systeem, waarom deze zijn gebruikt en wordt er gereflecteerd op de toegevoegde waarde & kosten van deze tools.

## Gebruikte ontwikkeltools (IDE's)
| Tool | Waarom | Reflectie |
|------|--------|-----------|
|VS code|Omgeving om te programeren|Bevat veel extensies en is goed te gebruiken voor meerdere code talen.|
|Git|Bied ons de mogelijkheid om te werken met branches|Een must have als je wilt programeren in een groep. Het zorgt ervoor dat iedereen kan werken aan zijn eigen stuk code.|
|Gidhub|Omgeving om alle git acties bij the houden en om taken te verdelen|Goede omgeving om in samen te werken, het checkt ook of het veilig is om branches te mergen.|
|Github desktop|Zorgt ervoor dat repositories en branches makkelijk te beheren zijn|Erg fijn om te gebruiken wanneer snel van branch moet wisselen of mergen.|
|Docker compose|draait lokaal alle componenten van de applicatie|Ingewikkeld om op te stellen, maar zorgt ervoor dat een hele omgeving op gestart kan worden.|
|Draw.io|Omgeving om diagrammen te maken|Erg handig en simpel te gebruiken om allerij soorten diagrammen te maken.|

## Gebruikte AI tools
| Tool | Waarom | Reflectie |
|------|--------|-----------|
|ChatGPT|Code genereren|De AI is meer gericht om jouw gelijk te geven inplaats van je echt te helpen. Hoewel het je (algemeen) werkende code geeft, is een AI zoals claude meer geschikt omdat het je specifiek vraagt om context.|
|Claude|Creeëren van diagrammen|Vanwege wat ik hierboven zei, is claude meer geschikt in het helpen van het maken in diagrammen.|
|Co-pilot|Uitleggen van de architectuur|Het kan het hele project bekijken en is daarom handig te gebruiken als je iets wilt weten in de applicatie.|

## Kosten en Toegevoegde Waarde AI-Tools
| Tool | Kosten | Toegevoegde Waarde(s) |
|------|--------|-----------|
|Alle AI|Mijn kennis op programeervaardigheid. Vanwege een mix van een korte deadline en meerdere dingen die we moeten toepassen, word er al snel gebruik gemaakt van AI. Dit zorgt ervoor dat er minder geprogrameerd wordt en minder kennis wordt opgehaald.|Het versneld het programeer werk.|

## Voorbeelden AI-tooling gebruik
- Een message provider implementeren met uitleg hoe het werkt.
- Het helpen maken van een C4 diagram en het helpen begrijpen van de hele structuur.

## Verbeterpunten
Na het reflecteren op de gebruikte tools, zijn een aantal verbeterpunten naar voren gekomen:
- Het gebruik van AI verminderen, omdat dit ervoor zorgt dat mijn programeervaardigheid achteruit gaat. Dit is makkelijker gezegd dan gedaan, want AI is makkelijk te gebruiken en wordt soms ook verwacht te worden bij opdrachten met korte deadlines.
- De communicatie in de verbeteren, ik zelf en andere leden in de groep hebben niet goed gecomminuceerd. Dit waren dingen als wie wat gedaan heeft, de vooruitgang van taken en het organiseren van meetings. We hadden concreet moeten afspreken wanneer en hoelaat we daily scrum's zouden hebben om betere communicatie te hebben.

## Bijdrage aan Project (Commits)
*Navigeer ook naar https://github.com/orgs/PatientPingeling/projects/2/views/1 en https://github.com/PatientPingeling/PatientPingeling/commits/main/ voor de op GitHub bijgehouden Project en de gehele commit geschiedenis*

| Issue/Onderdeel | Beschrijving | Datum |
|--------|-------------|-------|
|[Interface toegevoegd](https://github.com/PatientPingeling/PatientPingeling/commit/931dc9194f20d4231a7d6161cba0d5b625337fbd) |Dit is de interface die alle message providers kunnen gebruiken voor het bericht.|14-05-2026|
|[Swiftsend provider toegevoegd](https://github.com/PatientPingeling/PatientPingeling/commit/87d7e56eaa81f5844b2df67ddde65a8b08af96ba)|Er wordt een conectie gemaakt met de provider, die verwacht de benodigde variabelen. Een messageId wordt terug gegeven bij succes.|20-05-2026|
|[C4 container toegevoegd](https://github.com/PatientPingeling/PatientPingeling/commit/aa27ad2f3fab73a9435e2c5eb211bb443f051457)|Toont de relaties tussen de componenten in de applicatie (de diagram is later aangepast)|22-05-2026|
|[Proces diagram toegevoegd](https://github.com/PatientPingeling/PatientPingeling/commit/60aa80add0e0fb6445430c2510e8a73713e69443)|Toont de stappen die de applictie doet om een bericht naar een message provide te sturen|22-05-2026|
|RabbitMQListener geïnplementeerd (door iemand anders gedaan)|Dit hoorde de NotificationDispatchService aan te roepen|20-05-2026|
