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

- **File:** [src/NotificationService.Application/Commands/RabbitMQNotificationMessage.cs:11-13](src/NotificationService.Application/Commands/RabbitMQNotificationMessage.cs#L11-L13)
- **Severity:** Critical
- **Description:** `RabbitMQNotificationMessage` carries `PatientName`, `PatientEmail`, and `PatientPhone` in plaintext. This object is serialized to JSON and placed on the durable RabbitMQ queue. Anyone with RabbitMQ management console access, access to the disk where durable messages are persisted, or network access to the broker can read full patient PII. Under GDPR Art. 32, data must be protected with appropriate technical measures.
- **Impact:** Patient PII is fully exposed at rest in RabbitMQ storage and in transit between Scheduler and Worker. A single broker credential compromise exposes all patient data.
- **Fix:** Strip all PII from the queue message. Pass only `ScheduledNotificationId` + metadata needed for staleness checks. The Worker (which already creates a scoped DI scope per message) can fetch patient data directly from the DB just before dispatch.

---

#### [SEC-2] Provider spoofing — queue message `Provider` field not validated against tenant's DB record

- **File:** [src/NotificationService.Application/Services/NotificationDispatchService.cs:20-21](src/NotificationService.Application/Services/NotificationDispatchService.cs#L20-L21)
- **Severity:** Critical
- **Description:** The Worker reads `notificationMessage.Provider` directly from the RabbitMQ message and passes it to `_providerFactory.Create(...)` without validating it matches the tenant's actual configured provider in the database. A maliciously crafted or tampered message could specify a different provider and potentially route a dispatch through an unintended channel. There is an explicit TODO comment acknowledging this.
- **Impact:** Provider spoofing via queue message tampering. A compromised RabbitMQ broker or insider threat can cause notifications to be sent through the wrong provider using credentials from a different tenant's configuration.
- **Fix:** Before dispatching, look up `Tenant.Provider` from the DB using `notificationMessage.TenantId` and assert it equals `notificationMessage.Provider`. Reject the message if they diverge.

---

#### [SEC-3] Full EF `ProviderCredential` entity serialized to RabbitMQ — internal DB keys exposed

- **File:** [src/NotificationService.Application/Commands/RabbitMQNotificationMessage.cs:23](src/NotificationService.Application/Commands/RabbitMQNotificationMessage.cs#L23)
- **Severity:** High
- **Description:** `ProviderCredential[]` is the full EF entity, which includes `Id` (internal DB int), `TenantId`, and a `Tenant` navigation property (null at serialization, but fragile). While `EncryptedValue` is encrypted, exposing `Id` and `TenantId` leaks internal DB surrogate keys to the message broker. If `Tenant` navigation is ever loaded before serialization (e.g., an EF Include upstream), the full tenant record including `ApiKeyHash` could end up on the queue. There is an explicit TODO comment acknowledging this.
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
- **Fix:** Use an adaptive hashing algorithm: `Microsoft.AspNetCore.Cryptography.KeyDerivation.Pbkdf2` with a random per-tenant salt and >=100,000 iterations, or `BCrypt.Net`. Store `(Salt, Hash)` per tenant. The constant-time comparison already in place is correct — keep it.

---

#### [SEC-6] No rate limiting on the webhook endpoint — API key brute-force possible

- **File:** [src/NotificationService.Api/Endpoints/WebhookEndpoints.cs:42-47](src/NotificationService.Api/Endpoints/WebhookEndpoints.cs#L42-L47)
- **Severity:** Medium
- **Description:** `/webhooks/appointments` has no rate limiting. Each failed API key attempt returns HTTP 401 immediately with no throttle. An attacker can automate a dictionary attack against `X-Api-Key` for a known `X-Tenant-Id`.
- **Impact:** API keys can be brute-forced from the network.
- **Fix:** Add `app.UseRateLimiter()` (built into ASP.NET Core since .NET 7) with a fixed-window policy per IP and/or per tenant ID. Example: 60 requests/minute per IP.

---

#### [SEC-7] XML injection in LegacyLink SOAP request — `message` and `recipient` interpolated into raw XML

- **File:** [src/NotificationService.Infrastructure/Providers/LegacyLink/LegacyLinkProvider.cs:40-47](src/NotificationService.Infrastructure/Providers/LegacyLink/LegacyLinkProvider.cs#L40-L47)
- **Severity:** High
- **Description:** The SOAP request body is built via raw string interpolation. Neither `recipient` nor `message` is XML-escaped before interpolation. A patient phone number or appointment message containing `<`, `>`, `&`, or `]]>` will produce malformed XML (throwing `XmlException` on parsing) or, if crafted deliberately, inject additional XML elements into the SOAP envelope. The FluentValidation rules do not restrict XML-special characters in phone numbers or instructions fields.
- **Impact:** A phone number like `+31</PhoneNumber><MessageText>injected` would corrupt the XML document. Malicious input from a compromised EHR could inject arbitrary SOAP elements, manipulating the SOAP request sent to the provider.
- **Fix:** Build the XML via `XDocument`/`XElement` rather than raw string interpolation — `XDocument` escapes values automatically:
  ```csharp
  XNamespace ns = "http://legacylink.fakecomworld.com/v1";
  var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
      new XElement(ns + "SendSmsRequest",
          new XElement(ns + "PhoneNumber", recipient),
          new XElement(ns + "MessageText", message),
          new XElement(ns + "SenderIdentification", "NotificationService")));
  ```

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
- **Description:** `AuthenticateAsync` uses a check-then-act pattern: `TryGetValue` -> if miss, call `/auth`. Two concurrent dispatches for the same tenant can both miss the cache simultaneously and each make an `/auth` call.
- **Impact:** Minor: two simultaneous auth calls, both succeed independently. One token gets overwritten. Not a correctness issue, just wasteful.
- **Fix:** Use `GetOrCreateAsync` with a lock or a `SemaphoreSlim` per cache key to ensure only one `/auth` call in-flight per client.

---

#### [PERF-4] `GetPendingAsync` has no row limit — unbounded lock and memory usage under load

- **File:** [src/NotificationService.Infrastructure/Persistence/Repositories/ScheduledNotificationRepository.cs:69-98](src/NotificationService.Infrastructure/Persistence/Repositories/ScheduledNotificationRepository.cs#L69-L98)
- **Severity:** Medium
- **Description:** The raw SQL query in `GetPendingAsync` uses `FOR UPDATE SKIP LOCKED` but has no `LIMIT` clause. An explicit TODO comment in the code acknowledges this: `// TODO: ADD LIMIT OF 10 OR SOMETHING!`. If thousands of notifications become due simultaneously (e.g., after a scheduler outage), the query loads all of them into memory, takes row-level locks on all of them, and then the factory issues 2N DB queries for each (see PERF-1). The combination of unbounded lock scope and unbounded result set can stall other concurrent DB operations.
- **Impact:** During a large backlog drain, the Scheduler holds `FOR UPDATE` locks on every pending row simultaneously, blocking cancellation webhooks and other writes on those rows for the full poll cycle duration.
- **Fix:** Add `LIMIT 50` (or a configurable value) to the raw SQL query. Process notifications in bounded batches per poll cycle.

---

### 🟡 CONSISTENCY

---

#### [CONS-1] `PollerBackgroundService` constructor-injects scoped services — captive dependency

- **Files:** [src/NotificationService.Scheduler/Polling/PollBackgroundService.cs:4](src/NotificationService.Scheduler/Polling/PollBackgroundService.cs#L4), [src/NotificationService.Scheduler/Program.cs:17-21](src/NotificationService.Scheduler/Program.cs#L17-L21)
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
  Also verify `_transaction = null` at the end of `CommitAsync` and `RollbackAsync` (currently already done for CommitAsync — add for RollbackAsync's null-check path too).

---

#### [CONS-3] Dead interface `IRabbitMQNoticicationMessageFactory` with a double typo

- **File:** [src/NotificationService.Application/Abstractions/IRabbitMQNoticicationMessageFactory.cs](src/NotificationService.Application/Abstractions/IRabbitMQNoticicationMessageFactory.cs)
- **Severity:** Low
- **Description:** The interface is named `IRabbitMQNoticicationMessageFactory` ("Noticication"). The concrete implementation `NotificationMessageFactory` implements `INotificationMessageFactory` from `NotificationService.Application.Factories`. This dead interface is never registered in DI and never implemented. Additionally, `NotificationMessageFactory.cs` lives in the Application project but declares `namespace NotificationService.Infrastructure.Messaging` — a namespace/directory mismatch.
- **Impact:** Dead code. Misleads readers about the intended abstraction.
- **Fix:** Delete `IRabbitMQNoticicationMessageFactory.cs`. Fix the namespace in `NotificationMessageFactory.cs` to match its physical location (`NotificationService.Application.Services` or move the file to Infrastructure).

---

#### [CONS-4] `ProviderCredentialRepository` methods throw `NotImplementedException` — registered in DI

- **File:** [src/NotificationService.Infrastructure/Persistence/Repositories/ProviderCredentialRepository.cs:12-19](src/NotificationService.Infrastructure/Persistence/Repositories/ProviderCredentialRepository.cs#L12-L19)
- **Severity:** Medium
- **Description:** Both `AddAsync` and `DeleteByTenantAsync` on `ProviderCredentialRepository` unconditionally `throw new NotImplementedException()`. The repository is registered in DI in `InfrastructureExtensions.AddDatabase`. Any code path that resolves `IProviderCredentialRepository` and calls either method will crash at runtime with an unhandled exception.
- **Impact:** Any admin or tenant-management operation that adds or removes provider credentials will throw an unhandled exception at runtime, with no compile-time warning. Since the DI registration exists, callers assume the contract is fulfilled.
- **Fix:** Either implement the methods or remove the DI registration until the feature is ready. At minimum, remove the DI registration so that accidental injection fails early at startup rather than silently at call time.

---

### 🔴 STABILITY & RESILIENCE

---

#### [STAB-1] ✅ FIXED — Worker crashes on every message — `NullReferenceException` in `UnitOfWork.CommitAsync`

- **File:** [src/NotificationService.Infrastructure/Persistence/UnitOfWork.cs](src/NotificationService.Infrastructure/Persistence/UnitOfWork.cs)
- **Severity:** Critical
- **Status:** Fixed. `CommitAsync` now guards against a null `_transaction`. Worker paths that never call `BeginTransactionAsync` (EXPIRED, FAILED, SUCCESS) now only run `SaveChangesAsync` and ACK correctly. `_transaction` is also nulled after commit and rollback to prevent orphaned transactions.
- **Fix applied:**
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

#### [STAB-4] ✅ FIXED — No HTTP retry/backoff policy on provider HTTP clients

- **File:** [src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs:41-119](src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs#L41-L119)
- **Severity:** High
- **Status:** Fixed. All four provider HTTP clients (SwiftSend, SecurePost, AsyncFlow, LegacyLink) now call `.AddStandardResilienceHandler(...)` with configured exponential backoff, jitter, and circuit breakers per provider's risk profile.

---

#### [STAB-5] Notification permanently stuck in `INSCHEDULER` state on Scheduler crash

- **File:** [src/NotificationService.Scheduler/Polling/PollAction.cs:68-113](src/NotificationService.Scheduler/Polling/PollAction.cs#L68-L113)
- **Severity:** Medium
- **Description:** The Scheduler write sequence is: (1) write `INSCHEDULER`, (2) publish to RabbitMQ, (3) write `INQUEUE`. If the service crashes after step 2 but before step 3, the notification's latest `DispatchLog` outcome is `INSCHEDULER`. The `GetPendingAsync` query filters for `Outcome IN ('NEW', 'EXPIRED', 'ERROR_429')` — `INSCHEDULER` is not in the list, so the notification is never re-scheduled. The message IS on the queue (step 2 succeeded), so it will be processed eventually — but if the Worker also crashes before committing, the notification is permanently stuck.
- **Impact:** Notifications can become permanently invisible to the scheduler's recovery logic.
- **Fix:** Add a INSCHEDULER-recovery step: notifications with `INSCHEDULER` as their latest outcome and `AttemptedAt` older than N minutes should be reset to `NEW`. Alternatively, add `INSCHEDULER` to the recovery filter with a staleness window.

---

#### [STAB-6] ✅ FIXED — All dispatch failures mapped to `ERROR_429` regardless of actual error

- **File:** [src/NotificationService.Worker/HostedServices/RabbitMqNotificationConsumerService.cs:87-106](src/NotificationService.Worker/HostedServices/RabbitMqNotificationConsumerService.cs#L87-L106)
- **Severity:** Medium
- **Status:** Fixed. The Worker now distinguishes error types: `result.Error.Type == ErrorType.Failure` maps to `ERROR_429` (transient, requeue) while all other error types map to `ERROR_PERMANENT` (permanent, reject without requeue). `BasicNackAsync` is called with `requeue: transient` accordingly.
- **Fix applied:**
  ```csharp
  var transient = result.Error.Type == Domain.ErrorType.Failure;
  var outcome = transient ? Outcome.ERROR_429 : Outcome.ERROR_PERMANENT;
  await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: transient, cancellationToken: ct);
  ```

---

#### [STAB-7] RabbitMQ `BasicPublishAsync` has no publisher confirms — messages can be silently lost

- **File:** [src/NotificationService.Scheduler/RabbitMQ/RabbitMQEstablisher.cs:51-55](src/NotificationService.Scheduler/RabbitMQ/RabbitMQEstablisher.cs#L51-L55)
- **Severity:** Medium
- **Description:** `BasicPublishAsync` is called without enabling publisher confirms (`channel.ConfirmSelectAsync()`). RabbitMQ guarantees broker-side persistence only when the broker sends a `basic.ack` back to the publisher. Without confirms, the call returns as soon as the client writes bytes to the TCP socket. If the broker crashes or the TCP connection drops after the write but before the broker persists the message, the message is silently lost. The Scheduler then writes `INQUEUE`, leaving a notification stuck in `INQUEUE` with no corresponding queue message and no recovery path.
- **Impact:** Under a broker crash at exactly the wrong moment, a notification is permanently lost. The Scheduler believes it is `INQUEUE`; the Worker never sees it.
- **Fix:** Enable publisher confirms after channel creation:
  ```csharp
  await _channel.ConfirmSelectAsync();
  ```
  Then after `BasicPublishAsync`, call `await _channel.WaitForConfirmsOrDieAsync(ct)` to block until the broker acknowledges persistence.

---

### 🟡 CORRECTNESS & BUSINESS LOGIC

---

#### [CORR-1] Cascade delete silently destroys the `CANCELLED` DispatchLog audit trail

- **Files:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs:206-222](src/NotificationService.Application/Services/AppointmentIngestionService.cs#L206-L222)
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

#### [CORR-3] ✅ FIXED — Notifications created with past `SendAt` times dispatched with wrong context

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs](src/NotificationService.Application/Services/AppointmentIngestionService.cs)
- **Severity:** Medium
- **Status:** Fixed. Per team decision: past-dated `SendAt` values are **clamped to `now`** rather than dropped, so patients always receive a notification even when an appointment is rescheduled to within the next hour or day.
- **Fix applied:**
  ```csharp
  private static ScheduledNotification[] CreateScheduledNotifications(Appointment appointment, DateTimeOffset scheduledAt)
  {
      var now = DateTimeOffset.UtcNow;
      return
      [
          new() { Id = Guid.CreateVersion7(), SendAt = Clamp(scheduledAt.AddHours(-24), now), Appointment = appointment },
          new() { Id = Guid.CreateVersion7(), SendAt = Clamp(scheduledAt.AddHours(-1), now), Appointment = appointment }
      ];
  }

  private static DateTimeOffset Clamp(DateTimeOffset value, DateTimeOffset floor) =>
      value < floor ? floor : value;
  ```

---

#### [CORR-4] `HandleUpdateAsync` always issues a full patient UPDATE even when no patient fields changed

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs:155](src/NotificationService.Application/Services/AppointmentIngestionService.cs#L155)
- **Severity:** Low
- **Description:** `HandleUpdateAsync` always calls `_patientRepository.UpdateAsync(appointment.Patient, ct)` which issues a full `UPDATE Patients SET ... WHERE Id = ?` statement against every field in the entity. An explicit TODO comment acknowledges this: `// TODO: only update if patient fields actually changed`. If the EHR sends frequent `UPDATED` webhooks where only appointment details change (e.g., location updates), the DB is needlessly updated for the patient row on every call.
- **Impact:** Unnecessary write amplification on the `Patients` table. Any optimistic concurrency token on the patient entity added later will produce false conflicts.
- **Fix:** Compare incoming values against existing patient fields before calling `UpdateAsync`, or use EF's change tracking to detect actual modifications rather than calling `Update(entity)` unconditionally.

---

#### [CORR-5] `Security:EncryptionKey` not validated for correct AES key length at startup

- **File:** [src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs:146-151](src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs#L146-L151)
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

#### [CORR-6] `Patient.LastCommunicationAt` defaults to `DateTimeOffset.MinValue` — GDPR cleanup anonymizes newly created patients

- **File:** [src/NotificationService.Domain/Entities/Patient.cs:18](src/NotificationService.Domain/Entities/Patient.cs#L18), [src/NotificationService.Infrastructure/Persistence/Repositories/PatientRepository.cs:38-45](src/NotificationService.Infrastructure/Persistence/Repositories/PatientRepository.cs#L38-L45)
- **Severity:** Medium
- **Description:** `Patient.LastCommunicationAt` is declared as `public DateTimeOffset LastCommunicationAt { get; set; }` with no initializer, so it defaults to `DateTimeOffset.MinValue` (year 0001). The GDPR cleanup query in `AnonymizeStaleAsync` matches patients with `LastCommunicationAt < cutoff` (cutoff = `UtcNow - 14 days`). Any patient whose `LastCommunicationAt` was never explicitly set will immediately match the retention cutoff and be anonymized on the next cleanup run. A TODO comment in `Patient.cs` acknowledges this field needs attention: `// TODO: Fix this!`.
- **Impact:** A patient created via any code path that forgets to set `LastCommunicationAt` will be anonymized within 24 hours of creation, silently wiping their name, email, and phone. Future notifications to that patient will fail with empty contact data.
- **Fix:** Assign a sensible default in the entity:
  ```csharp
  public DateTimeOffset LastCommunicationAt { get; set; } = DateTimeOffset.UtcNow;
  ```
  Also verify that all patient creation paths explicitly set this field (CREATED and UPDATED handlers already do; CANCELLED does not, but that code path does not create patients).

---

#### [CORR-7] `HandleUpdateAsync` deletes pending notifications without writing `CANCELLED` DispatchLogs

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs:159-173](src/NotificationService.Application/Services/AppointmentIngestionService.cs#L159-L173)
- **Severity:** Medium
- **Description:** When an appointment is rescheduled (time changes), `HandleUpdateAsync` calls `DeletePendingByAppointmentIdAsync` to delete the old `ScheduledNotification` rows, then creates new ones with new `SendAt` times. Unlike `HandleCancelledAsync`, it does not write `DispatchLog(CANCELLED)` entries for the deleted notifications before removing them. The old scheduled notifications are deleted with no audit record of why they were removed.
- **Impact:** The dispatch log has a gap: a notification went from `NEW` to being deleted with no recorded reason. This breaks the audit trail for rescheduled appointments and makes debugging dispatch failures harder.
- **Fix:** Before calling `DeletePendingByAppointmentIdAsync` in the reschedule path, call `GetPendingIdsByAppointmentIdAsync` and insert a `DispatchLog(CANCELLED)` for each affected pending notification ID, mirroring what `HandleCancelledAsync` does. Note: this must be committed before the delete to avoid the cascade issue described in CORR-1.

---

#### [CORR-8] `HandleUpdateAsync` returns 404 for unknown appointments — should upsert

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs:135-139](src/NotificationService.Application/Services/AppointmentIngestionService.cs#L135-L139)
- **Severity:** Medium
- **Description:** When an `UPDATED` webhook arrives for an appointment that does not yet exist in the database, `HandleUpdateAsync` returns a `404 NotFound` error and discards the event entirely. EHR systems (including OpenMRS) frequently emit `UPDATED` events before `CREATED` events due to race conditions, event ordering issues, or replay scenarios. The current behaviour silently drops the appointment, meaning the patient never gets scheduled for notifications.
- **Impact:** Appointment data is silently lost whenever OpenMRS delivers an `UPDATED` event out of order. The patient receives no notifications for that appointment. There is no error visible to the caller other than a 404.
- **Fix:** Implement upsert logic in `HandleUpdateAsync`: if the appointment is not found, delegate to `HandleCreatedAsync` (or extract the creation logic into a shared method) rather than returning `NotFound`. This matches the intent described in GitHub issue [#51](https://github.com/PatientPingeling/PatientPingeling/issues/51).

---

## SUMMARY TABLE

| ID     | Category    | Severity     | File (primary)                                                | Description                                                      |
| ------ | ----------- | ------------ | ------------------------------------------------------------- | ---------------------------------------------------------------- |
| SEC-1  | Security    | **Critical** | RabbitMQNotificationMessage.cs:11                             | Patient PII in plaintext on queue                                |
| SEC-2  | Security    | **Critical** | NotificationDispatchService.cs:20                             | Provider spoofing via queue message                              |
| STAB-1 | Stability   | ✅ **Fixed** | UnitOfWork.cs                                                 | Worker crashes every message — NullReferenceException            |
| SEC-3  | Security    | **High**     | RabbitMQNotificationMessage.cs:23                             | EF entity ProviderCredential with DB keys on queue               |
| SEC-4  | Security    | **High**     | appsettings.json (all 3)                                      | Default credentials committed to git                             |
| SEC-5  | Security    | **High**     | Sha256HashingService.cs:9                                     | Unsalted SHA-256 for API key hashing                             |
| SEC-7  | Security    | **High**     | LegacyLinkProvider.cs:40                                      | XML injection in SOAP request body                               |
| PERF-1 | Performance | **High**     | NotificationMessageFactory.cs:23                              | N+1 (2N) DB queries per poll cycle                               |
| CONS-1 | Consistency | **High**     | PollBackgroundService.cs:4                                    | Captive scoped dependency in singleton background service        |
| STAB-2 | Stability   | **High**     | PollBackgroundService.cs:10                                   | No RabbitMQ reconnection in Scheduler                            |
| STAB-3 | Stability   | **High**     | RabbitMqNotificationConsumerService.cs:25                     | No RabbitMQ reconnection in Worker                               |
| STAB-4 | Stability   | ✅ **Fixed** | InfrastructureExtentions.cs:41                                | HTTP resilience handlers now on all 4 provider clients           |
| CORR-1 | Correctness | **High**     | AppointmentIngestionService.cs:206                            | Cascade delete destroys CANCELLED audit logs                     |
| SEC-6  | Security    | **Medium**   | WebhookEndpoints.cs:42                                        | No rate limiting on webhook endpoint                             |
| PERF-2 | Performance | **Medium**   | PatientRepository.cs:12, AppointmentRepository.cs:11          | Missing composite indexes on (ExternalId, TenantId)              |
| PERF-4 | Performance | **Medium**   | ScheduledNotificationRepository.cs:97                         | GetPendingAsync has no row limit — unbounded under load          |
| CONS-2 | Consistency | **Medium**   | UnitOfWork.cs:11                                              | BeginTransactionAsync orphans existing transaction               |
| CONS-4 | Consistency | **Medium**   | ProviderCredentialRepository.cs:12                            | AddAsync/DeleteByTenantAsync throw NotImplementedException        |
| STAB-5 | Stability   | **Medium**   | PollAction.cs:68                                              | INSCHEDULER state not in recovery filter — permanent stuck       |
| STAB-6 | Stability   | ✅ **Fixed** | RabbitMqNotificationConsumerService.cs:87                     | ERROR_PERMANENT now distinct from ERROR_429 — no infinite retry  |
| STAB-7 | Stability   | **Medium**   | RabbitMQEstablisher.cs:51                                     | No publisher confirms — messages can be silently lost            |
| CORR-2 | Correctness | **Medium**   | AppointmentIngestionService.cs:141                            | UPDATE nullifies patient contact data when fields omitted        |
| CORR-3 | Correctness | ✅ **Fixed** | AppointmentIngestionService.cs                                | Past-dated SendAt clamped to now — immediate dispatch            |
| CORR-6 | Correctness | **Medium**   | Patient.cs:18, PatientRepository.cs:38                        | LastCommunicationAt defaults to MinValue — premature GDPR wipe   |
| CORR-7 | Correctness | **Medium**   | AppointmentIngestionService.cs:159                            | Reschedule path deletes notifications with no CANCELLED log      |
| CORR-8 | Correctness | **Medium**   | AppointmentIngestionService.cs:135                            | UPDATED webhook returns 404 for unknown appointments — should upsert |
| CORR-4 | Correctness | **Low**      | AppointmentIngestionService.cs:155                            | Patient always updated in HandleUpdate even if unchanged         |
| CORR-5 | Correctness | **Low**      | InfrastructureExtentions.cs:146                               | Encryption key length not validated at startup                   |
| CONS-3 | Consistency | **Low**      | IRabbitMQNoticicationMessageFactory.cs                        | Dead interface with typo; namespace mismatch                     |
| PERF-3 | Performance | **Low**      | SecurePostProvider.cs:64                                      | Thundering herd double-check in IMemoryCache auth                |

---

## TOP PRIORITY FIXES

1. ~~**STAB-1** — Fix `UnitOfWork.CommitAsync` — Worker crashed on every message.~~ ✅ Fixed
2. ~~**CORR-3** — Past-dated `SendAt` values dispatched with wrong context.~~ ✅ Fixed — clamped to `now`
3. ~~**STAB-4** — No HTTP retry/backoff on provider clients.~~ ✅ Fixed — `AddStandardResilienceHandler` on all four provider clients
4. ~~**STAB-6** — All failures mapped to ERROR_429, causing infinite retry loops.~~ ✅ Fixed — ERROR_PERMANENT now distinct and rejected without requeue
5. **SEC-1 + SEC-2 + SEC-3** — Redesign `RabbitMQNotificationMessage` to carry only IDs + metadata, strip PII and entity references. These three findings share one root cause.
6. **CORR-1** — Fix the cascade delete destroying cancellation audit trail (stop deleting ScheduledNotifications on cancel, or change FK to RESTRICT).
7. **CORR-6** — Fix `Patient.LastCommunicationAt` default before the GDPR cleanup accidentally anonymizes real patients.
8. **SEC-7** — Fix XML injection in `LegacyLinkProvider` by building the SOAP envelope via `XDocument`.
9. **CONS-1** — Fix captive dependency in Scheduler by using `IServiceScopeFactory` per poll cycle.
10. **STAB-2 + STAB-3** — Enable `AutomaticRecoveryEnabled = true` on `ConnectionFactory` for both services.
11. **STAB-7** — Enable RabbitMQ publisher confirms in `RabbitMQEstablisher` to prevent silent message loss.
12. **PERF-4** — Add a `LIMIT` clause to `GetPendingAsync` to bound memory and lock scope per poll cycle.
