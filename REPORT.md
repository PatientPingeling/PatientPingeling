# PatientPingeling — Full Codebase Audit Report

## Context

Full audit of the PatientPingeling .NET 10 medical notification system across five dimensions: Security, Performance, Consistency, Stability & Resilience, and Correctness & Business Logic. The system processes appointment webhooks via an API, queues notifications through RabbitMQ, and dispatches them via external SMS/email providers. It handles patient PII and must comply with GDPR.

Audit covered all source files across:

- `NotificationService.Api` — REST API, webhook ingestion
- `NotificationService.Application` — services, commands, abstractions
- `NotificationService.Domain` — entities, enums, Result<T>
- `NotificationService.Infrastructure` — EF Core, repositories, providers, security
- `NotificationService.Scheduler` — polls DB, publishes to RabbitMQ
- `NotificationService.Worker` — consumes RabbitMQ, dispatches

---

## FINDINGS BY CATEGORY

---

### 🔴 SECURITY

---

#### [SEC-1] Patient PII in plaintext on RabbitMQ queue

- **File:** [src/NotificationService.Application/Commands/RabbitMQNotificationMessage.cs:12-14](src/NotificationService.Application/Commands/RabbitMQNotificationMessage.cs#L12-L14)
- **Severity:** Critical
- **Description:** `RabbitMQNotificationMessage` carries `PatientName`, `PatientEmail`, and `PatientPhone` in plaintext. This object is serialized to JSON and placed on the durable RabbitMQ queue. Anyone with RabbitMQ management console access, access to the disk where durable messages are persisted, or network access to the broker can read full patient PII. Under GDPR Art. 32, data must be protected with appropriate technical measures.
- **Impact:** Patient PII is fully exposed at rest in RabbitMQ storage and in transit between Scheduler and Worker. A single broker credential compromise exposes all patient data.
- **Fix:** Strip all PII from the queue message. Pass only `ScheduledNotificationId` + metadata needed for staleness checks. The Worker (which already creates a scoped DI scope per message) can fetch patient data directly from the DB just before dispatch.

---

#### [SEC-2] Provider spoofing — queue message `Provider` field not validated against tenant's DB record

- **File:** [src/NotificationService.Application/Services/NotificationDispatchService.cs:20-21](src/NotificationService.Application/Services/NotificationDispatchService.cs#L20-L21)
- **Severity:** Critical
- **Description:** The Worker reads `notificationMessage.Provider` directly from the RabbitMQ message and passes it to `_providerFactory.Create(...)` without validating it matches the tenant's actual configured provider in the database. A maliciously crafted or tampered message could specify a different provider and potentially route a dispatch through an unintended channel. There is already a TODO comment acknowledging this.
- **Impact:** Provider spoofing via queue message tampering. A compromised RabbitMQ broker or insider threat can cause notifications to be sent through the wrong provider using credentials from a different tenant's configuration.
- **Fix:** Before dispatching, look up `Tenant.Provider` from the DB using `notificationMessage.TenantId` and assert it equals `notificationMessage.Provider`. Reject the message if they diverge.

---

#### [SEC-3] Full EF `ProviderCredential` entity serialized to RabbitMQ — internal DB keys exposed

- **File:** [src/NotificationService.Application/Commands/RabbitMQNotificationMessage.cs:23](src/NotificationService.Application/Commands/RabbitMQNotificationMessage.cs#L23)
- **Severity:** High
- **Description:** `ProviderCredential[]` is the full EF entity, which includes `Id` (internal DB int), `TenantId`, and a `Tenant` navigation property (null at serialization, but fragile). While `EncryptedValue` is encrypted, exposing `Id` and `TenantId` leaks internal DB surrogate keys to the message broker. If `Tenant` navigation is ever loaded before serialization (e.g., an EF Include upstream), the full tenant record including `ApiKeyHash` could end up on the queue. There is already a TODO comment acknowledging this.
- **Impact:** Internal DB keys leaked to the broker. Future refactors that eager-load `Tenant` would silently leak `ApiKeyHash` values to the queue.
- **Fix:** Create a dedicated DTO: `record CredentialDto(string Key, string EncryptedValue)` and map from the entity at the point of factory creation. Remove the entity reference from `RabbitMQNotificationMessage`.

---

#### [SEC-4] Default credentials committed to appsettings.json in source control

- **Files:** [src/NotificationService.Api/appsettings.json:14-18](src/NotificationService.Api/appsettings.json#L14-L18), [src/NotificationService.Worker/appsettings.json:9-13](src/NotificationService.Worker/appsettings.json#L9-L13), [src/NotificationService.Scheduler/appsettings.json:9-13](src/NotificationService.Scheduler/appsettings.json#L9-L13)
- **Severity:** High
- **Description:** All three `appsettings.json` files commit hardcoded credentials: RabbitMQ `guest:guest` and PostgreSQL `postgres:postgres`. These live in the git history and are included in any Docker image built from the repo. The connection strings also expose host, port, and database name.
- **Impact:** If the repository or Docker image is ever shared, examined, or published, full broker and database credentials are visible.
- **Fix:** Replace credential values with empty strings or remove the fields entirely. Document required environment variables (`RabbitMQ__Password`, `ConnectionStrings__Postgres`, etc.) in a `README` or `.env.example`. Provision real credentials through Docker Compose `environment:` or Kubernetes secrets only.

---

#### [SEC-5] API key hashed with unsalted SHA-256 — vulnerable to rainbow table attacks

- **File:** [src/NotificationService.Infrastructure/Security/Sha256HashingService.cs:9-14](src/NotificationService.Infrastructure/Security/Sha256HashingService.cs#L9-L14)
- **Severity:** High
- **Description:** `Sha256HashingService.Hash()` computes a raw, unsalted SHA-256 of the API key. Two tenants with the same API key produce the same hash. A pre-computed rainbow table for common strings covers `"test-secret"` (the dev key) trivially.
- **Impact:** If the `Tenants` table is read by an attacker, all API keys with short or common values can be reversed via precomputed tables.
- **Fix:** Use an adaptive hashing algorithm: `Microsoft.AspNetCore.Cryptography.KeyDerivation.Pbkdf2` with a random per-tenant salt and ≥100,000 iterations, or `BCrypt.Net`. Store `(Salt, Hash)` per tenant. The constant-time comparison already in place is correct — keep it.

---

#### [SEC-6] No rate limiting on the webhook endpoint — API key brute-force possible

- **File:** [src/NotificationService.Api/Endpoints/WebhookEndpoints.cs:42-47](src/NotificationService.Api/Endpoints/WebhookEndpoints.cs#L42-L47)
- **Severity:** Medium
- **Description:** `/webhooks/appointments` has no rate limiting. Each failed API key attempt returns HTTP 401 immediately with no throttle. An attacker can automate a dictionary attack against `X-Api-Key` for a known `X-Tenant-Id`.
- **Impact:** API keys can be brute-forced from the network.
- **Fix:** Add `app.UseRateLimiter()` (built into ASP.NET Core since .NET 7) with a fixed-window policy per IP and/or per tenant ID. Example: 60 requests/minute per IP.

---

### 🟠 PERFORMANCE

---

#### [PERF-1] N+1 query in `NotificationMessageFactory.CreateAsync`

- **File:** [src/NotificationService.Application/Services/NotificationMessageFactory.cs:23-38](src/NotificationService.Application/Services/NotificationMessageFactory.cs#L23-L38)
- **Severity:** High
- **Description:** For every element of the `scheduledNotifications[]` array, the factory executes two sequential DB queries: (1) `GetLatestStatusByScheduledApointmentIdASync` and (2) `GetByIdWithDetailsAsync`. For N notifications, this is **2N roundtrips** executed serially. The scheduler fetches all pending notifications in one query but then immediately fans out to N×2 individual queries.
- **Impact:** Poll cycle time grows linearly with queue depth. At 50 pending notifications = 100 sequential DB roundtrips per minute. Under load this will cause poll cycles to overlap, starving the DB connection pool.
- **Fix:** Batch both queries. Load all latest `DispatchLog` entries in one `WHERE ScheduledNotificationId IN (...)` query, filter qualifying IDs in memory, then load all details with a single `WHERE Id IN (...)` with appropriate includes.

---

#### [PERF-2] Missing composite indexes on Patient and Appointment lookup columns

- **Files:** [src/NotificationService.Infrastructure/Persistence/Repositories/PatientRepository.cs:12-13](src/NotificationService.Infrastructure/Persistence/Repositories/PatientRepository.cs#L12-L13), [src/NotificationService.Infrastructure/Persistence/Repositories/AppointmentRepository.cs:11-13](src/NotificationService.Infrastructure/Persistence/Repositories/AppointmentRepository.cs#L11-L13)
- **Severity:** Medium
- **Description:** `GetByExternalIdAsync` on both Patient and Appointment filters on `(ExternalId, TenantId)`. The current indexes are only on `TenantId`. Without a composite index `(ExternalId, TenantId)`, every webhook request performs a full index scan over all patients/appointments of a tenant to match the external ID.
- **Impact:** Lookup latency grows linearly with the number of patients/appointments per tenant. Every webhook request (hot path) takes this hit.
- **Fix:** Add to `NotificationDbContext.OnModelCreating`:
  ```csharp
  entity.HasIndex(e => new { e.ExternalId, e.TenantId });
  ```
  for both `Patient` and `Appointment`.

---

#### [PERF-3] `IMemoryCache` thundering-herd double-check in `SecurePostProvider`

- **File:** [src/NotificationService.Infrastructure/Providers/SecurePost/SecurePostProvider.cs:64-80](src/NotificationService.Infrastructure/Providers/SecurePost/SecurePostProvider.cs#L64-L80)
- **Severity:** Low
- **Description:** `AuthenticateAsync` uses a check-then-act pattern: `TryGetValue` → if miss, call `/auth`. Two concurrent dispatches for the same tenant can both miss the cache simultaneously and each make an `/auth` call.
- **Impact:** Minor: two simultaneous auth calls, both succeed independently. One token gets overwritten. Not a correctness issue, just wasteful.
- **Fix:** Use `GetOrCreateAsync` with a lock or a `SemaphoreSlim` per cache key to ensure only one `/auth` call in-flight per client.

---

### 🟡 CONSISTENCY

---

#### [CONS-1] `PollerBackgroundService` constructor-injects scoped services — captive dependency

- **Files:** [src/NotificationService.Scheduler/Polling/PollBackgroundService.cs:4](src/NotificationService.Scheduler/Polling/PollBackgroundService.cs#L4), [src/NotificationService.Scheduler/Program.cs:17-20](src/NotificationService.Scheduler/Program.cs#L17-L20)
- **Severity:** High
- **Description:** `PollerBackgroundService` is a hosted service (effectively singleton). It constructor-injects `PollAction` and `RabbitMQEstablisher`, both registered as `Scoped`. .NET DI resolves scoped services from the root container when injected into a singleton, creating a single instance shared across all poll iterations for the process lifetime. The captured `NotificationDbContext` (inside `PollAction`) is never disposed and accumulates tracked entities indefinitely. In Development, `ValidateScopes = true` will throw an `InvalidOperationException` at startup.
- **Impact:** Memory leak (EF change tracker grows forever). If two poll cycles ever overlap (poll takes > 60s), the shared non-thread-safe `DbContext` will corrupt.
- **Fix:** Mirror the Worker's correct pattern: inject `IServiceScopeFactory` into the background service, create a new `IServiceScope` at the start of each poll cycle, resolve `PollAction` from that scope, dispose the scope after the poll.

---

#### [CONS-2] `UnitOfWork.BeginTransactionAsync` silently orphans existing active transaction

- **File:** [src/NotificationService.Infrastructure/Persistence/UnitOfWork.cs:11-13](src/NotificationService.Infrastructure/Persistence/UnitOfWork.cs#L11-L13)
- **Severity:** Medium
- **Description:** `BeginTransactionAsync` unconditionally assigns `_transaction = await _dbContext.Database.BeginTransactionAsync(ct)`. If called when a transaction is already active (as happens in `PollAction` which calls Begin/Commit in a loop), the previous `_transaction` reference is overwritten without being committed or rolled back. The old transaction object is orphaned.
- **Impact:** On exception between `SaveChangesAsync` and `CommitAsync`, the old transaction is abandoned but PostgreSQL holds the connection lock until it times out. Can cause connection pool starvation.
- **Fix:** Add a guard:
  ```csharp
  if (_transaction is not null)
      throw new InvalidOperationException("A transaction is already active.");
  ```
  Also add `_transaction = null` at the end of `CommitAsync` and `RollbackAsync`.

---

#### [CONS-3] Dead interface `IRabbitMQNoticicationMessageFactory` with a double typo

- **File:** [src/NotificationService.Application/Abstractions/IRabbitMQNoticicationMessageFactory.cs](src/NotificationService.Application/Abstractions/IRabbitMQNoticicationMessageFactory.cs)
- **Severity:** Low
- **Description:** The interface is named `IRabbitMQNoticicationMessageFactory` ("Noticication"). The concrete implementation `NotificationMessageFactory` implements `INotificationMessageFactory` from `NotificationService.Application.Factories`. This dead interface is never registered in DI and never implemented. Additionally, `NotificationMessageFactory.cs` lives in the Application project but declares `namespace NotificationService.Infrastructure.Messaging` — a namespace/directory mismatch.
- **Impact:** Dead code. Misleads readers about the intended abstraction.
- **Fix:** Delete `IRabbitMQNoticicationMessageFactory.cs`. Fix the namespace in `NotificationMessageFactory.cs` to match its physical location (`NotificationService.Application.Services` or move the file to Infrastructure).

---

### 🔴 STABILITY & RESILIENCE

---

#### [STAB-1] Worker crashes on every message — `NullReferenceException` in `UnitOfWork.CommitAsync`

- **File:** [src/NotificationService.Worker/HostedServices/RabbitMqNotificationConsumerService.cs:70-73, 83-97, 101-121](src/NotificationService.Worker/HostedServices/RabbitMqNotificationConsumerService.cs#L70-L121), [src/NotificationService.Infrastructure/Persistence/UnitOfWork.cs:18](src/NotificationService.Infrastructure/Persistence/UnitOfWork.cs#L18)
- **Severity:** Critical
- **Description:** Every code path in the Worker (EXPIRED, FAILED, SUCCESS) calls `unitOfWork.CommitAsync()` without a prior `BeginTransactionAsync()`. `UnitOfWork.CommitAsync` does:

  ```csharp
  await _dbContext.SaveChangesAsync(ct);   // ← succeeds (auto-commit)
  await _transaction!.CommitAsync(ct);     // ← NullReferenceException (_transaction is null)
  ```

  The `!` operator does not prevent the null — it only suppresses the compiler warning. The `NullReferenceException` is caught by the outer `catch (Exception ex)` which calls `BasicNackAsync(requeue: true)`.

  **Consequence chain for a SUCCESS dispatch:**
  1. Notification IS dispatched to provider (HTTP call succeeded)
  2. `DispatchLog(SUCCESS)` and `NotificationLog` ARE written to DB (via `SaveChangesAsync`)
  3. Worker crashes on `CommitAsync` → outer catch → message **requeued**
  4. Worker receives same message again → dispatches again (double-send)
  5. Repeat until `MessageSla` is exceeded (but the EXPIRED path also crashes and requeues → infinite loop)

- **Impact:** Every notification is dispatched multiple times. Patients receive duplicate SMS/emails indefinitely. The system can never successfully ACK a message. All three dispatch paths (SUCCESS, FAIL, EXPIRED) are broken.
- **Fix (immediate):** Change `CommitAsync` to skip the transaction commit when no explicit transaction was started:
  ```csharp
  public async Task CommitAsync(CancellationToken ct = default)
  {
      await _dbContext.SaveChangesAsync(ct);
      if (_transaction is not null)
      {
          await _transaction.CommitAsync(ct);
          _transaction = null;
      }
  }
  ```
  For paths that genuinely need atomicity, call `BeginTransactionAsync` explicitly before writing.

---

#### [STAB-2] Scheduler RabbitMQ connection established once — no reconnection on broker outage

- **File:** [src/NotificationService.Scheduler/Polling/PollBackgroundService.cs:10](src/NotificationService.Scheduler/Polling/PollBackgroundService.cs#L10), [src/NotificationService.Scheduler/RabbitMQ/RabbitMQEstablisher.cs:21-37](src/NotificationService.Scheduler/RabbitMQ/RabbitMQEstablisher.cs#L21-L37)
- **Severity:** High
- **Description:** `EstablishConnection()` is called once at service startup. If RabbitMQ becomes unavailable and recovers, the `_channel` is in a closed/faulted state. `PublishAsync` throws `AlreadyClosedException`, which is caught in `PollAction` and rolls back to `NEW` — but the channel itself is never re-established.
- **Impact:** Any transient RabbitMQ outage permanently disables notification dispatch until the Scheduler container is manually restarted.
- **Fix:** Wrap `PublishAsync` with a reconnection check (`_connection?.IsOpen == false`). Add a retry loop with exponential backoff in `EstablishConnection`. Alternatively, use the RabbitMQ.Client `ConnectionFactory` with `AutomaticRecoveryEnabled = true`.

---

#### [STAB-3] Worker RabbitMQ connection established once — same single-connection pattern

- **File:** [src/NotificationService.Worker/HostedServices/RabbitMqNotificationConsumerService.cs:25-26](src/NotificationService.Worker/HostedServices/RabbitMqNotificationConsumerService.cs#L25-L26)
- **Severity:** High
- **Description:** `await using var connection = await _connectionFactory.CreateConnectionAsync(ct)` at the top of `ExecuteAsync` means the connection is created once. If it drops, the consumer silently stops receiving messages — the `ReceivedAsync` event is never fired again. No reconnection logic exists.
- **Impact:** Same as STAB-2 — a transient RabbitMQ outage permanently silences the Worker.
- **Fix:** Enable `AutomaticRecoveryEnabled = true` on the `ConnectionFactory` in `InfrastructureExtensions.AddMessageBroker`, or wrap `ExecuteAsync` body in a retry loop that reconnects and re-registers the consumer.

---

#### [STAB-4] No HTTP retry/backoff policy on provider HTTP clients

- **File:** [src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs:48-49](src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs#L48-L49) (explicit TODO comment)
- **Severity:** High
- **Description:** Provider `HttpClient` registrations have no resilience policy. A single 429 or 503 from a provider throws `HttpRequestException`, which `NotificationDispatchService` catches and returns as `ErrorType.Failure`. The Worker then nacks with `requeue: true`. Without backoff, the message immediately returns and is retried at full speed, hammering the provider. There is a TODO comment explicitly acknowledging this for `AddStandardResilienceHandler()`.
- **Impact:** A provider returning 429 causes an unbounded tight retry loop that violates the provider's rate limit and fills the queue.
- **Fix:** Add `Microsoft.Extensions.Http.Resilience` and call `.AddStandardResilienceHandler()` on each `AddHttpClient(...)` call. This adds exponential backoff, circuit breaking, and jitter out of the box.

---

#### [STAB-5] Notification permanently stuck in `INSCHEDULER` state on Scheduler crash

- **File:** [src/NotificationService.Scheduler/Polling/PollAction.cs:68-113](src/NotificationService.Scheduler/Polling/PollAction.cs#L68-L113)
- **Severity:** Medium
- **Description:** The Scheduler write sequence is: (1) write `INSCHEDULER`, (2) publish to RabbitMQ, (3) write `INQUEUE`. If the service crashes after step 2 but before step 3, the notification's latest `DispatchLog` outcome is `INSCHEDULER`. The `GetPendingAsync` query filters for `Outcome IN ('NEW', 'EXPIRED', 'ERROR_429')` — `INSCHEDULER` is not in the list, so the notification is never re-scheduled. The message IS on the queue (step 2 succeeded), so it will be processed eventually — but if the Worker also crashes before committing (see STAB-1), the notification is permanently stuck.
- **Impact:** Notifications can become permanently invisible to the scheduler's recovery logic.
- **Fix:** Add a INSCHEDULER-recovery step: notifications with `INSCHEDULER` as their latest outcome and `AttemptedAt` older than N minutes should be reset to `NEW`. Alternatively, add `INSCHEDULER` to the recovery filter with a staleness window.

---

#### [STAB-6] All dispatch failures mapped to `ERROR_429` regardless of actual error

- **File:** [src/NotificationService.Worker/HostedServices/RabbitMqNotificationConsumerService.cs:83-87](src/NotificationService.Worker/HostedServices/RabbitMqNotificationConsumerService.cs#L83-L87)
- **Severity:** Medium
- **Description:** When dispatch fails, the Worker always writes `Outcome = Outcome.ERROR_429` — even for permanent failures (bad payload, no contact info, missing credentials). The `Outcome.ERROR_429` value signals to the Scheduler that a retry is appropriate (`'ERROR_429'` is in the `GetPendingAsync` recovery filter). Permanent failures will be retried indefinitely.
- **Impact:** Permanently-failing notifications (bad config, missing contact data) loop forever through the Scheduler → queue → Worker → fail → queue cycle, consuming resources.
- **Fix:** Distinguish error outcomes. Use `ERROR_TRANSIENT` (or keep `ERROR_429`) for transient failures and introduce `ERROR_PERMANENT` for permanent ones. Only include `ERROR_429` in the scheduler's recovery filter, not `ERROR_PERMANENT`.

---

### 🟡 CORRECTNESS & BUSINESS LOGIC

---

#### [CORR-1] Cascade delete silently destroys the `CANCELLED` DispatchLog audit trail

- **Files:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs:205-222](src/NotificationService.Application/Services/AppointmentIngestionService.cs#L205-L222), [src/NotificationService.Infrastructure/Migrations/20260520153538_DispatchLogAdded.cs:39-44](src/NotificationService.Infrastructure/Migrations/20260520153538_DispatchLogAdded.cs#L39-L44)
- **Severity:** High
- **Description:** `HandleCancelledAsync` writes `DispatchLog(CANCELLED)` for each pending notification, then calls `DeletePendingByAppointmentIdAsync` to delete the `ScheduledNotification` rows. The FK `FK_DispatchLogs_ScheduledNotifications_ScheduledNotificationId` is defined with `onDelete: ReferentialAction.Cascade`. EF Core processes all changes in a single `SaveChangesAsync`:
  1. INSERT the new `DispatchLog(CANCELLED)` rows
  2. DELETE the `ScheduledNotification` rows → CASCADE deletes the just-inserted `DispatchLog` rows

  Net result: no cancellation record survives in the database. The audit trail is silently destroyed.

- **Impact:** GDPR audit trail for cancellations is absent. Operationally, there is no record that a notification was ever scheduled for a cancelled appointment.
- **Fix (option A — preferred):** Do not delete `ScheduledNotification` rows on cancellation. Setting `appointment.IsCancelled = true` already prevents the Scheduler from re-queuing them (the `GetPendingAsync` query filters `IsCancelled = FALSE`). Keep the rows for audit history.
  **Fix (option B):** Change the FK to `RESTRICT`, write and commit the `CANCELLED` logs in a first transaction, then delete the `ScheduledNotification` rows in a second transaction.

---

#### [CORR-2] UPDATE webhook nullifies patient email/phone when fields are omitted

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs:141-143](src/NotificationService.Application/Services/AppointmentIngestionService.cs#L141-L143)
- **Severity:** Medium
- **Description:** In `HandleUpdateAsync`:
  ```csharp
  appointment.Patient.Email = command.Patient.Email ?? string.Empty;
  appointment.Patient.PhoneNumber = command.Patient.PhoneNumber ?? string.Empty;
  ```
  If an UPDATE webhook omits `email` (sends `null`), the patient's stored email is overwritten with `""`. Future notifications cannot be sent to that patient.
- **Impact:** A partial-update webhook (common in EHR systems that only emit changed fields) silently erases contact data, breaking all future notification delivery for that patient.
- **Fix:** Only update fields that are explicitly provided:
  ```csharp
  if (command.Patient.Email is not null)
      appointment.Patient.Email = command.Patient.Email;
  if (command.Patient.PhoneNumber is not null)
      appointment.Patient.PhoneNumber = command.Patient.PhoneNumber;
  ```

---

#### [CORR-3] Notifications created with past `SendAt` times are dispatched with wrong context

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs:261-267](src/NotificationService.Application/Services/AppointmentIngestionService.cs#L261-L267)
- **Severity:** Medium
- **Description:** `CreateScheduledNotifications` unconditionally creates two reminders at `scheduledAt - 24h` and `scheduledAt - 1h`. If an appointment is scheduled within the next 24 hours (e.g., `ScheduledAt = now + 30 min`), the 24-hour reminder has `SendAt = now - 23.5h` which is already in the past. The Scheduler picks it up immediately (`SendAt <= now`). The Worker's staleness check uses `EnqueuedAt` (set when published, i.e., `now`), not `SendAt`, so the message does **not** expire. The patient receives a "24-hour reminder" after their appointment time.
- **Impact:** Patients receive contextually incorrect notifications (a "reminder" sent after the appointment). GDPR impact: unnecessary processing of patient data for no purpose.
- **Fix:** Filter out past-dated notifications in `CreateScheduledNotifications`:
  ```csharp
  var now = DateTimeOffset.UtcNow;
  return candidates.Where(n => n.SendAt > now).ToArray();
  ```
  Optionally also validate at the API level that `ScheduledAt > now + 1h` to prevent ingestion of imminent-past appointments.

---

#### [CORR-4] `HandleUpdateAsync` timezone comparison bug — may miss schedule changes

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs:149-157](src/NotificationService.Application/Services/AppointmentIngestionService.cs#L149-L157)
- **Severity:** Medium
- **Description:**

  ```csharp
  appointment.ScheduledAt = command.Appointment.ScheduledAt.ToUniversalTime();  // stored as UTC
  ...
  if (oldScheduledAt != command.Appointment.ScheduledAt)  // ← compares UTC vs original offset
  ```

  `oldScheduledAt` is a UTC `DateTimeOffset`. `command.Appointment.ScheduledAt` may have a non-zero UTC offset (e.g., `+01:00`). Two `DateTimeOffset` values representing the same instant but different offsets are **not equal** by reference equality — they ARE equal via `DateTimeOffset.Equals(a, b)` which normalises to UTC. But `!=` on `DateTimeOffset` uses `Equals`, which IS UTC-aware. So actually this is correct for `DateTimeOffset`.

  Wait — actually this is fine if both operands are `DateTimeOffset`. `DateTimeOffset` equality compares the UTC instant, not the offset. The comparison is correct. Downgrading this finding.

  **Revised:** This is not a bug for `DateTimeOffset`. No fix needed.

---

#### [CORR-5] `Security:EncryptionKey` not validated for correct AES key length at startup

- **File:** [src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs:92-97](src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs#L92-L97)
- **Severity:** Low
- **Description:** The key is read as Base64 and decoded, but its length is not validated. AES-GCM requires exactly 16, 24, or 32 bytes. A misconfigured key causes a `CryptographicException` at the first credential encryption/decryption call, not at startup, making the failure hard to diagnose.
- **Impact:** Misconfigured encryption key produces a cryptic runtime exception during the seeding or first provider credential operation, not during application boot.
- **Fix:**
  ```csharp
  var keyBytes = Convert.FromBase64String(key);
  if (keyBytes.Length is not (16 or 24 or 32))
      throw new InvalidOperationException("Security:EncryptionKey must decode to 16, 24, or 32 bytes.");
  return new AesGcmEncryptionService(keyBytes);
  ```

---

## SUMMARY TABLE

| ID     | Category    | Severity     | File (primary)                                            | Description                                                  |
| ------ | ----------- | ------------ | --------------------------------------------------------- | ------------------------------------------------------------ |
| SEC-1  | Security    | **Critical** | RabbitMQNotificationMessage.cs:12                         | Patient PII in plaintext on queue                            |
| SEC-2  | Security    | **Critical** | NotificationDispatchService.cs:21                         | Provider spoofing via queue message                          |
| STAB-1 | Stability   | **Critical** | RabbitMqNotificationConsumerService.cs + UnitOfWork.cs:18 | Worker crashes every message → double-dispatch infinite loop |
| SEC-3  | Security    | **High**     | RabbitMQNotificationMessage.cs:23                         | EF entity ProviderCredential with DB keys on queue           |
| SEC-4  | Security    | **High**     | appsettings.json (all 3)                                  | Default credentials committed to git                         |
| SEC-5  | Security    | **High**     | Sha256HashingService.cs:9                                 | Unsalted SHA-256 for API key hashing                         |
| PERF-1 | Performance | **High**     | NotificationMessageFactory.cs:23                          | N+1 (2N) DB queries per poll cycle                           |
| CONS-1 | Consistency | **High**     | PollBackgroundService.cs:4                                | Captive scoped dependency in singleton background service    |
| STAB-2 | Stability   | **High**     | PollBackgroundService.cs:10                               | No RabbitMQ reconnection in Scheduler                        |
| STAB-3 | Stability   | **High**     | RabbitMqNotificationConsumerService.cs:25                 | No RabbitMQ reconnection in Worker                           |
| STAB-4 | Stability   | **High**     | InfrastructureExtentions.cs:48                            | No HTTP retry/backoff on provider clients                    |
| CORR-1 | Correctness | **High**     | AppointmentIngestionService.cs:205                        | Cascade delete destroys CANCELLED audit logs                 |
| SEC-6  | Security    | **Medium**   | WebhookEndpoints.cs:42                                    | No rate limiting on webhook endpoint                         |
| PERF-2 | Performance | **Medium**   | PatientRepository.cs:12, AppointmentRepository.cs:11      | Missing composite indexes on (ExternalId, TenantId)          |
| CONS-2 | Consistency | **Medium**   | UnitOfWork.cs:11                                          | BeginTransactionAsync orphans existing transaction           |
| STAB-5 | Stability   | **Medium**   | PollAction.cs:68                                          | INSCHEDULER state not in recovery filter → permanent stuck   |
| STAB-6 | Stability   | **Medium**   | RabbitMqNotificationConsumerService.cs:84                 | All failures mapped to ERROR_429 — permanent errors retried  |
| CORR-2 | Correctness | **Medium**   | AppointmentIngestionService.cs:141                        | UPDATE nullifies patient contact data when fields omitted    |
| CORR-3 | Correctness | **Medium**   | AppointmentIngestionService.cs:261                        | Past-dated notifications dispatched without filtering        |
| CORR-5 | Correctness | **Low**      | InfrastructureExtentions.cs:92                            | Encryption key length not validated at startup               |
| CONS-3 | Consistency | **Low**      | IRabbitMQNoticicationMessageFactory.cs                    | Dead interface with typo; namespace mismatch                 |
| PERF-3 | Performance | **Low**      | SecurePostProvider.cs:64                                  | Thundering herd double-check in IMemoryCache auth            |

---

## TOP PRIORITY FIXES

1. **STAB-1** — Fix `UnitOfWork.CommitAsync` first. This is actively causing double-dispatch of every notification. Single-line fix.
2. **SEC-1 + SEC-2 + SEC-3** — Redesign `RabbitMQNotificationMessage` to carry only IDs + metadata, strip PII and entity references. These three findings share one root cause.
3. **CORR-1** — Fix the cascade delete destroying cancellation audit trail (either stop deleting ScheduledNotifications, or change FK to RESTRICT).
4. **CONS-1** — Fix captive dependency in Scheduler by using `IServiceScopeFactory` per poll cycle.
5. **STAB-2 + STAB-3** — Enable `AutomaticRecoveryEnabled = true` on `ConnectionFactory` for both services.
