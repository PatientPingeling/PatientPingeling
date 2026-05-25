# Testrapportage — Performance Testen (k6)

## Inleiding

Dit document beschrijft de performance testen van de **PatientPingeling Notification API**. Het doel van deze testen is aantonen dat de architectuur (Notification API + scheduler + worker + RabbitMQ + Postgres) bij realistische webhook-belasting binnen de gestelde response-time en error-rate budgetten blijft.

Het verschil met de andere testlagen:

- **Unit tests** bewijzen dat de businesslogica per klasse klopt.
- **Integration tests** bewijzen dat componenten samenwerken met echte infrastructuur via Testcontainers.
- **System tests (Bruno)** bewijzen dat de happy paths vanaf buiten werken op de Docker stack.
- **Performance tests (k6)** bewijzen dat de stack onder **gelijktijdige belasting** stabiel blijft en de afgesproken latency-budgetten haalt.

---

## Uitvoeren

**Vereisten:** Docker Compose stack actief (`docker compose up --build`), API bereikbaar op `localhost:8000`.

```bash
# macOS / Linux
./scripts/run-perf-test.sh

# Windows (PowerShell)
.\scripts\run-perf-test.ps1
```

De wrapper haalt het k6-image uit Docker Hub, mount het testscript in de container en stuurt requests naar `host.docker.internal:8000` zodat de test container met de host-stack praat zonder dat we de compose-file hoeven aan te passen.

Overrides via environment variables:

```bash
BASE_URL=http://localhost:8000 \
TENANT_ID=3fa85f64-5717-4562-b3fc-2c963f66afa6 \
API_KEY=test-secret \
./scripts/run-perf-test.sh
```

---

## Testopzet

| Gegeven                | Waarde                                                                |
| ---------------------- | --------------------------------------------------------------------- |
| Testtool               | k6 (Grafana)                                                          |
| Testscript             | [`tests/performance/webhook-load.js`](../../tests/performance/webhook-load.js) |
| Endpoint onder test    | `POST /webhooks/appointments`                                         |
| Belastingprofiel       | 30s ramp-up → 1m sustain @ 20 VUs → 30s ramp-down                     |
| Virtuele gebruikers    | Tot 20 gelijktijdig                                                   |
| Iteraties per gebruiker| Onbeperkt binnen het tijdvenster                                      |
| Payload                | Unieke `CREATED` webhook per iteratie (unieke patient + appointment) |
| Drempelwaarden         | `http_req_failed < 1%` &nbsp;·&nbsp; `http_req_duration p(95) < 500ms` |

### Waarom dit profiel?

- **20 VUs is realistisch voor een SaaS-notificatiemodule** die door meerdere OpenMRS-instanties wordt aangeroepen. Een hoger getal kies je pas als je productie-cijfers hebt.
- **De 1m sustain-fase** is lang genoeg voor de scheduler om in actie te komen en voor het asynchrone gedrag (queue groei, worker-doorvoer) zichtbaar te worden in Grafana.
- **Unieke IDs per request** voorkomen dat we de duplicate-shortcut in de ingestion service raken — dan zou je de DB-write-pad niet écht testen.

---

## Resultaten

<!-- TODO: vul deze sectie na het runnen van de test op basis van de k6-output. -->

| Metriek                        | Waarde |
| ------------------------------ | ------ |
| Totaal aantal requests         | _TODO_ |
| Geslaagde requests (HTTP 201)  | _TODO_ |
| Gefaalde requests              | _TODO_ |
| Error rate                     | _TODO_ |
| Throughput (req/s, gemiddeld)  | _TODO_ |
| `http_req_duration` p(50)      | _TODO_ |
| `http_req_duration` p(95)      | _TODO_ |
| `http_req_duration` p(99)      | _TODO_ |
| `http_req_duration` max        | _TODO_ |
| Drempel p(95) < 500ms          | _PASS / FAIL_ |
| Drempel error rate < 1%        | _PASS / FAIL_ |

### Grafana-bewijs

Tijdens de test was de realtime monitoring zichtbaar op `http://localhost:3000`. Screenshots zijn opgenomen in [`Screenshots/`](Screenshots/):

<!-- TODO: voeg screenshots toe en link ze hier in, bv:
- ![throughput](Screenshots/perf-throughput.png)
- ![queue depth](Screenshots/perf-queue-depth.png)
- ![error rate](Screenshots/perf-error-rate.png)
-->

---

## Analyse

<!-- TODO: vul na de eerste run in. Vragen om te beantwoorden:
- Waar zat de bottleneck? (API CPU? DB writes? RabbitMQ publish? Worker dispatch?)
- Was de queue-depth stabiel of liep 'ie op?
- Schaalde Postgres connectie-pool mee?
- Wat gebeurt er bij 50 VUs in plaats van 20?
-->

---

## Verbeterstappen

<!-- TODO: documenteer 1-2 concrete tweaks die je op basis van de eerste run hebt gedaan.
Voorbeelden om over na te denken:
- Connection pool size aanpassen
- `BasicQos prefetchCount` op de worker verhogen
- Index toegevoegd op een hot-query
- Caching aangezet voor tenant lookup
Dit onderdeel telt voor de Goed-score op rubric "Betrouwbaarheid".
-->

---

## Conclusie

<!-- TODO: vul na de run in. Sjabloon:
De stack haalde onder 20 VU belasting een gemiddelde throughput van X req/s met een p(95) latency van Yms en een error rate van Z%. Beide gestelde drempelwaarden zijn [wel/niet] gehaald. De voornaamste verbetering die op basis van de eerste run is doorgevoerd betreft [bottleneck + fix].
-->
