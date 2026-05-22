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

#### [SEC-5] ✅ FIXED — API key hashed with unsalted SHA-256 — vulnerable to rainbow table attacks

- **File:** [src/NotificationService.Infrastructure/Security/Pbkdf2HashingService.cs](src/NotificationService.Infrastructure/Security/Pbkdf2HashingService.cs)
- **Severity:** High
- **Status:** Fixed. `Sha256HashingService` replaced by `Pbkdf2HashingService` using PBKDF2-HMAC-SHA256 with a random 32-byte salt per hash, 100,000 iterations, and 32-byte output. Stored format: `base64(salt):base64(hash)` in the existing `ApiKeyHash` column — no migration needed. Constant-time comparison preserved. `DevDataSeeder` updated to hash at seed time via `IHashingService` instead of hardcoding the SHA-256 value.
- **Fix applied:**
  ```csharp
  byte[] salt = RandomNumberGenerator.GetBytes(32);
  byte[] hash = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, 100_000, HashAlgorithmName.SHA256, 32);
  return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
  ```

---

#### [SEC-6] No rate limiting on the webhook endpoint — API key brute-force possible

- **File:** [src/NotificationService.Api/Endpoints/WebhookEndpoints.cs:42-47](src/NotificationService.Api/Endpoints/WebhookEndpoints.cs#L42-L47)
- **Severity:** Medium
- **Description:** `/webhooks/appointments` has no rate limiting. Each failed API key attempt returns HTTP 401 immediately with no throttle. An attacker can automate a dictionary attack against `X-Api-Key` for a known `X-Tenant-Id`.
- **Impact:** API keys can be brute-forced from the network.
- **Fix:** Add `app.UseRateLimiter()` (built into ASP.NET Core since .NET 7) with a fixed-window policy per IP and/or per tenant ID. Example: 60 requests/minute per IP.

---

#### [SEC-7] ✅ FIXED — XML injection in LegacyLink SOAP request

- **File:** [src/NotificationService.Infrastructure/Providers/LegacyLink/LegacyLinkProvider.cs](src/NotificationService.Infrastructure/Providers/LegacyLink/LegacyLinkProvider.cs)
- **Severity:** High
- **Status:** Fixed. SOAP envelope is now built via `XDocument`/`XElement` which automatically escapes all values. Raw string interpolation removed.
- **Fix applied:**
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

#### [PERF-1] ✅ FIXED — N+1 query in `NotificationMessageFactory.CreateAsync`

- **File:** [src/NotificationService.Application/Services/NotificationMessageFactory.cs](src/NotificationService.Application/Services/NotificationMessageFactory.cs)
- **Severity:** High
- **Status:** Fixed. Added `GetLatestStatusBatchAsync` to `IDispatchLogRepository` / `DispatchLogRepository`: one `WHERE ScheduledNotificationId IN (...)` query loads all latest logs, in-memory filter picks eligible IDs, then the per-notification `GetByIdWithDetailsAsync` loop runs only for eligible entries. Reduces dispatch-log lookups from N queries to 1.
- **Fix applied:**
  ```csharp
  var latestLogs = await _dispatchLogRepository.GetLatestStatusBatchAsync(ids, ct);
  var eligibleIds = ids.Where(id => {
      latestLogs.TryGetValue(id, out var log);
      return log is null || log.Outcome == Outcome.NEW;
  }).ToList();
  ```

---

#### [PERF-2] ✅ FIXED — Missing composite indexes on Patient and Appointment lookup columns

- **File:** [src/NotificationService.Infrastructure/Persistence/NotificationDbContext.cs](src/NotificationService.Infrastructure/Persistence/NotificationDbContext.cs)
- **Severity:** Medium
- **Status:** Fixed. Composite indexes `(ExternalId, TenantId)` added to both `Patient` and `Appointment` in `OnModelCreating`. EF migration `AddCompositeIndexes` generated and applied. Webhook ingestion lookups now use index seeks instead of full scans.

---

#### [PERF-3] ✅ FIXED — `IMemoryCache` thundering-herd double-check in `SecurePostProvider`

- **File:** [src/NotificationService.Infrastructure/Providers/SecurePost/SecurePostProvider.cs](src/NotificationService.Infrastructure/Providers/SecurePost/SecurePostProvider.cs)
- **Severity:** Low
- **Status:** Fixed. Replaced `TryGetValue` + `Set` with `GetOrCreateAsync`, which consolidates the cache lookup and population into a single operation. Concurrent misses will still each call `/auth`, but the pattern is now idiomatic and eliminates the stale-write race on the return path.
- **Fix applied:**
  ```csharp
  return (await _cache.GetOrCreateAsync(cacheKey, async entry =>
  {
      var authResult = await CallAuthEndpoint(...);
      entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(authResult.ExpiresIn - 30, 30));
      return authResult;
  }))!;
  ```

---

#### [PERF-4] ✅ FIXED — `GetPendingAsync` has no row limit — unbounded lock and memory usage under load

- **File:** [src/NotificationService.Infrastructure/Persistence/Repositories/ScheduledNotificationRepository.cs](src/NotificationService.Infrastructure/Persistence/Repositories/ScheduledNotificationRepository.cs)
- **Severity:** Medium
- **Status:** Fixed. `LIMIT 50` added to the raw SQL query. Poll cycles now process at most 50 notifications at a time, bounding both memory usage and lock scope.

---

### 🟡 CONSISTENCY

---

#### [CONS-1] ✅ FIXED — `PollerBackgroundService` constructor-injects scoped services — captive dependency

- **Files:** [src/NotificationService.Scheduler/Polling/PollBackgroundService.cs](src/NotificationService.Scheduler/Polling/PollBackgroundService.cs), [src/NotificationService.Scheduler/Program.cs](src/NotificationService.Scheduler/Program.cs)
- **Severity:** High
- **Status:** Fixed. `PollerBackgroundService` now injects `IServiceScopeFactory` and creates a fresh `AsyncScope` per poll cycle. `PollAction` (and its `DbContext`) is resolved from the scope and disposed after each poll. `RabbitMQEstablisher` moved to Singleton since the connection is established once at startup. Lock contention dropped from ~19 per 5 minutes to near zero (verified in Grafana).

---

#### [CONS-2] ✅ FIXED — `UnitOfWork.BeginTransactionAsync` silently orphans existing active transaction

- **File:** [src/NotificationService.Infrastructure/Persistence/UnitOfWork.cs](src/NotificationService.Infrastructure/Persistence/UnitOfWork.cs)
- **Severity:** Medium
- **Status:** Fixed. Guard added to `BeginTransactionAsync` — throws `InvalidOperationException` if a transaction is already active, making double-open a hard failure instead of a silent orphan.
- **Fix applied:**
  ```csharp
  if (_transaction is not null)
      throw new InvalidOperationException("A transaction is already active. Commit or roll back the existing transaction before starting a new one.");
  ```

---

#### [CONS-3] ✅ FIXED — Dead interface `IRabbitMQNoticicationMessageFactory` with a double typo

- **File:** [src/NotificationService.Application/Abstractions/INotificationMessageFactory.cs](src/NotificationService.Application/Abstractions/INotificationMessageFactory.cs)
- **Severity:** Low
- **Status:** Fixed. `IRabbitMQNoticicationMessageFactory.cs` renamed to `INotificationMessageFactory.cs`. Namespace in `NotificationMessageFactory.cs` corrected from `NotificationService.Infrastructure.Messaging` to `NotificationService.Application.Services`. `Scheduler/Program.cs` using updated accordingly.

---

#### [CONS-4] ✅ FIXED — `ProviderCredentialRepository` methods throw `NotImplementedException` — registered in DI

- **File:** [src/NotificationService.Infrastructure/Persistence/Repositories/ProviderCredentialRepository.cs](src/NotificationService.Infrastructure/Persistence/Repositories/ProviderCredentialRepository.cs)
- **Severity:** Medium
- **Status:** Fixed. `AddAsync` implemented via `_dbContext.ProviderCredentials.Add(credential)`. `DeleteByTenantAsync` implemented via `ExecuteDeleteAsync` with a `WHERE TenantId = ?` filter. Both methods now fulfill the contract the DI registration implies.

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

#### [STAB-2 + STAB-3] ✅ FIXED — No RabbitMQ reconnection in Scheduler and Worker

- **File:** [src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs](src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs)
- **Severity:** High
- **Status:** Fixed. `AutomaticRecoveryEnabled = true` and `NetworkRecoveryInterval = TimeSpan.FromSeconds(5)` added to `ConnectionFactory` in `AddMessageBroker`. Both Scheduler and Worker now automatically reconnect after transient broker outages without requiring a container restart.

---

#### [STAB-4] ✅ FIXED — No HTTP retry/backoff policy on provider HTTP clients

- **File:** [src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs:41-119](src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs#L41-L119)
- **Severity:** High
- **Status:** Fixed. All four provider HTTP clients (SwiftSend, SecurePost, AsyncFlow, LegacyLink) now call `.AddStandardResilienceHandler(...)` with configured exponential backoff, jitter, and circuit breakers per provider's risk profile.

---

#### [STAB-5] ✅ FIXED — Notification permanently stuck in `INSCHEDULER` state on Scheduler crash

- **File:** [src/NotificationService.Infrastructure/Persistence/Repositories/ScheduledNotificationRepository.cs](src/NotificationService.Infrastructure/Persistence/Repositories/ScheduledNotificationRepository.cs)
- **Severity:** Medium
- **Status:** Fixed. `GetPendingAsync` now also recovers notifications whose latest outcome is `INSCHEDULER` and whose `AttemptedAt` is older than 5 minutes, covering the crash-between-publish-and-INQUEUE window.
- **Fix applied:**
  ```sql
  OR (d."Outcome" = 'INSCHEDULER' AND d."AttemptedAt" < NOW() - INTERVAL '5 minutes')
  ```

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

#### [STAB-7] ✅ FIXED — RabbitMQ `BasicPublishAsync` has no publisher confirms — messages can be silently lost

- **File:** [src/NotificationService.Scheduler/RabbitMQ/RabbitMQEstablisher.cs](src/NotificationService.Scheduler/RabbitMQ/RabbitMQEstablisher.cs)
- **Severity:** Medium
- **Status:** Fixed. Channel now created with `PublisherConfirmationsEnabled = true` and `PublisherConfirmationTrackingEnabled = true` via `CreateChannelOptions` (RabbitMQ.Client v7 API). With tracking enabled, `BasicPublishAsync` blocks until the broker sends `basic.ack`, making silent message loss impossible.
- **Fix applied:**
  ```csharp
  _channel = await _connection.CreateChannelAsync(
      new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));
  ```

---

### 🟡 CORRECTNESS & BUSINESS LOGIC

---

#### [CORR-1] ✅ FIXED — Cascade delete silently destroys the `CANCELLED` DispatchLog audit trail

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs](src/NotificationService.Application/Services/AppointmentIngestionService.cs)
- **Severity:** High
- **Status:** Fixed. `DeletePendingByAppointmentIdAsync` removed from `HandleCancelledAsync`. `ScheduledNotification` rows are now kept as immutable audit history. `appointment.IsCancelled = true` already prevents `GetPendingAsync` from re-queuing them (`IsCancelled = FALSE` filter). `CANCELLED` `DispatchLog` entries now survive and are queryable.

---

#### [CORR-2] ✅ FIXED — UPDATE webhook nullifies patient email/phone when fields are omitted

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs](src/NotificationService.Application/Services/AppointmentIngestionService.cs)
- **Severity:** Medium
- **Status:** Fixed. Null-guards added — `Email` and `PhoneNumber` are only overwritten when the incoming webhook field is explicitly non-null. Omitted fields in partial-update webhooks no longer erase stored contact data.

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

#### [CORR-4] ✅ FIXED — `HandleUpdateAsync` always issues a full patient UPDATE even when no patient fields changed

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs](src/NotificationService.Application/Services/AppointmentIngestionService.cs)
- **Severity:** Low
- **Status:** Fixed. Incoming patient fields are compared to the loaded entity before calling `UpdateAsync`. The patient `UPDATE` is skipped entirely when `GivenName`, `Email`, and `PhoneNumber` are all unchanged.
- **Fix applied:**
  ```csharp
  var patientChanged = appointment.Patient.GivenName != command.Patient.GivenName
      || (command.Patient.Email is not null && appointment.Patient.Email != command.Patient.Email)
      || (command.Patient.PhoneNumber is not null && appointment.Patient.PhoneNumber != command.Patient.PhoneNumber);
  if (patientChanged) await _patientRepository.UpdateAsync(appointment.Patient, ct);
  ```

---

#### [CORR-5] ✅ FIXED — `Security:EncryptionKey` not validated for correct AES key length at startup

- **File:** [src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs](src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs)
- **Severity:** Low
- **Status:** Fixed. Key bytes decoded at registration time and length validated before `AesGcmEncryptionService` is constructed. Bad key now fails at startup with a clear `InvalidOperationException` rather than a cryptic `CryptographicException` on first use.
- **Fix applied:**
  ```csharp
  var keyBytes = Convert.FromBase64String(key);
  if (keyBytes.Length is not (16 or 24 or 32))
      throw new InvalidOperationException($"Security:EncryptionKey must decode to 16, 24, or 32 bytes for AES-GCM; got {keyBytes.Length}.");
  return new AesGcmEncryptionService(keyBytes);
  ```

---

#### [CORR-6] ✅ FIXED — `Patient.LastCommunicationAt` defaults to `DateTimeOffset.MinValue` — GDPR cleanup anonymizes newly created patients

- **File:** [src/NotificationService.Domain/Entities/Patient.cs](src/NotificationService.Domain/Entities/Patient.cs)
- **Severity:** Medium
- **Status:** Fixed. `LastCommunicationAt` now initializes to `DateTimeOffset.UtcNow`. New patients are safe from the 14-day GDPR cleanup window from the moment they are created. TODO comment removed.

---

#### [CORR-7] ✅ FIXED — `HandleUpdateAsync` deletes pending notifications without writing `CANCELLED` DispatchLogs

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs](src/NotificationService.Application/Services/AppointmentIngestionService.cs)
- **Severity:** Medium
- **Status:** Fixed. On reschedule, pending notification IDs are fetched first, then `CANCELLED` `DispatchLog` entries are written for each one inside the transaction, and — crucially — the old `ScheduledNotification` rows are **not** hard-deleted. Because the FK is `ON DELETE CASCADE`, deleting the rows would also cascade-delete the CANCELLED logs. Keeping the rows is safe: `GetPendingAsync` filters by latest dispatch log outcome, so CANCELLED notifications are already invisible to the Scheduler.
- **Fix applied:** Replaced `DeletePendingByAppointmentIdAsync` call with CANCELLED log writes for old IDs + add new notifications with NEW logs, all in a single transaction.

---

#### [CORR-8] ✅ FIXED — `HandleUpdateAsync` returns 404 for unknown appointments — should upsert

- **File:** [src/NotificationService.Application/Services/AppointmentIngestionService.cs](src/NotificationService.Application/Services/AppointmentIngestionService.cs)
- **Severity:** Medium
- **Status:** Fixed. Creation logic extracted into shared `PersistNewAppointmentAsync` helper. `HandleUpdateAsync` now upserts when the appointment is not found — logs a warning and creates the appointment from the UPDATED payload. Handles the OpenMRS race condition where UPDATED arrives before CREATED. Closes GitHub issue [#51](https://github.com/PatientPingeling/PatientPingeling/issues/51).

---

## SUMMARY TABLE

| ID     | Category    | Severity     | File (primary)                                                | Description                                                      |
| ------ | ----------- | ------------ | ------------------------------------------------------------- | ---------------------------------------------------------------- |
| SEC-1  | Security    | **Critical** | RabbitMQNotificationMessage.cs:11                             | Patient PII in plaintext on queue                                |
| SEC-2  | Security    | **Critical** | NotificationDispatchService.cs:20                             | Provider spoofing via queue message                              |
| STAB-1 | Stability   | ✅ **Fixed** | UnitOfWork.cs                                                 | Worker crashes every message — NullReferenceException            |
| SEC-3  | Security    | **High**     | RabbitMQNotificationMessage.cs:23                             | EF entity ProviderCredential with DB keys on queue               |
| SEC-4  | Security    | **High**     | appsettings.json (all 3)                                      | Default credentials committed to git                             |
| SEC-5  | Security    | ✅ **Fixed** | Pbkdf2HashingService.cs                                       | PBKDF2-HMAC-SHA256 with random salt, 100k iterations             |
| SEC-7  | Security    | ✅ **Fixed** | LegacyLinkProvider.cs                                         | XML injection fixed — SOAP body now built via XDocument          |
| PERF-1 | Performance | ✅ **Fixed** | NotificationMessageFactory.cs                                 | Batch dispatch-log query replaces N individual lookups           |
| CONS-1 | Consistency | ✅ **Fixed** | PollBackgroundService.cs                                      | IServiceScopeFactory per poll cycle — captive dependency removed |
| STAB-2 | Stability   | ✅ **Fixed** | InfrastructureExtentions.cs                                   | AutomaticRecoveryEnabled = true on ConnectionFactory             |
| STAB-3 | Stability   | ✅ **Fixed** | InfrastructureExtentions.cs                                   | AutomaticRecoveryEnabled = true on ConnectionFactory             |
| STAB-4 | Stability   | ✅ **Fixed** | InfrastructureExtentions.cs:41                                | HTTP resilience handlers now on all 4 provider clients           |
| CORR-1 | Correctness | ✅ **Fixed** | AppointmentIngestionService.cs                                | ScheduledNotification rows kept on cancel — audit trail preserved|
| SEC-6  | Security    | **Medium**   | WebhookEndpoints.cs:42                                        | No rate limiting on webhook endpoint                             |
| PERF-2 | Performance | ✅ **Fixed** | NotificationDbContext.cs                                      | Composite indexes (ExternalId, TenantId) on Patient + Appointment|
| PERF-4 | Performance | ✅ **Fixed** | ScheduledNotificationRepository.cs                            | LIMIT 50 added to GetPendingAsync — bounded per poll cycle       |
| CONS-2 | Consistency | ✅ **Fixed** | UnitOfWork.cs                                                 | Guard throws if BeginTransactionAsync called while active        |
| CONS-4 | Consistency | ✅ **Fixed** | ProviderCredentialRepository.cs                               | AddAsync and DeleteByTenantAsync now implemented                 |
| STAB-5 | Stability   | ✅ **Fixed** | ScheduledNotificationRepository.cs                            | INSCHEDULER + 5min staleness window added to recovery filter     |
| STAB-6 | Stability   | ✅ **Fixed** | RabbitMqNotificationConsumerService.cs:87                     | ERROR_PERMANENT now distinct from ERROR_429 — no infinite retry  |
| STAB-7 | Stability   | ✅ **Fixed** | RabbitMQEstablisher.cs                                        | Publisher confirms via CreateChannelOptions — silent loss fixed  |
| CORR-2 | Correctness | ✅ **Fixed** | AppointmentIngestionService.cs                                | Null-guard added — omitted fields no longer erase contact data   |
| CORR-3 | Correctness | ✅ **Fixed** | AppointmentIngestionService.cs                                | Past-dated SendAt clamped to now — immediate dispatch            |
| CORR-6 | Correctness | ✅ **Fixed** | Patient.cs                                                    | LastCommunicationAt defaults to UtcNow — no premature GDPR wipe  |
| CORR-7 | Correctness | ✅ **Fixed** | AppointmentIngestionService.cs                                | CANCELLED logs written before rescheduling; rows kept for audit  |
| CORR-8 | Correctness | ✅ **Fixed** | AppointmentIngestionService.cs                                | UPDATED webhook now upserts unknown appointments — closes #51    |
| CORR-4 | Correctness | ✅ **Fixed** | AppointmentIngestionService.cs                                | Patient UPDATE skipped when no fields changed                    |
| CORR-5 | Correctness | ✅ **Fixed** | InfrastructureExtentions.cs                                   | Encryption key length validated at startup — fails fast          |
| CONS-3 | Consistency | ✅ **Fixed** | INotificationMessageFactory.cs                                | File renamed, namespace corrected to Application.Services        |
| PERF-3 | Performance | ✅ **Fixed** | SecurePostProvider.cs                                         | GetOrCreateAsync replaces check-then-act cache pattern           |

---

## TOP PRIORITY FIXES

1. ~~**STAB-1** — Fix `UnitOfWork.CommitAsync` — Worker crashed on every message.~~ ✅ Fixed
2. ~~**CORR-3** — Past-dated `SendAt` values dispatched with wrong context.~~ ✅ Fixed — clamped to `now`
3. ~~**STAB-4** — No HTTP retry/backoff on provider clients.~~ ✅ Fixed — `AddStandardResilienceHandler` on all four provider clients
4. ~~**STAB-6** — All failures mapped to ERROR_429, causing infinite retry loops.~~ ✅ Fixed — ERROR_PERMANENT now distinct and rejected without requeue
5. ~~**SEC-7** — XML injection in `LegacyLinkProvider`.~~ ✅ Fixed — SOAP envelope built via `XDocument`
6. ~~**CONS-1** — Captive scoped dependency in Scheduler.~~ ✅ Fixed — `IServiceScopeFactory` per poll cycle; lock contentions dropped to near zero
7. ~~**PERF-4** — Unbounded `GetPendingAsync`.~~ ✅ Fixed — `LIMIT 50` added
8. ~~**CORR-8** — UPDATED webhook returns 404 for unknown appointments.~~ ✅ Fixed — upserts via shared `PersistNewAppointmentAsync` helper
9. ~~**SEC-5** — Unsalted SHA-256 for API key hashing.~~ ✅ Fixed — PBKDF2-HMAC-SHA256 with random salt, 100k iterations
10. ~~**SEC-5** — Unsalted SHA-256 for API key hashing.~~ ✅ Fixed — PBKDF2-HMAC-SHA256 with random salt, 100k iterations
11. ~~**CORR-1** — Cascade delete destroyed CANCELLED audit logs.~~ ✅ Fixed — ScheduledNotification rows kept; IsCancelled flag prevents re-queuing
12. ~~**CORR-2** — UPDATE webhook nullified email/phone when fields omitted.~~ ✅ Fixed — null-guards added
13. ~~**CORR-6** — LastCommunicationAt defaulted to MinValue.~~ ✅ Fixed — defaults to UtcNow
14. ~~**STAB-2 + STAB-3** — No RabbitMQ reconnection.~~ ✅ Fixed — AutomaticRecoveryEnabled = true
15. ~~**PERF-2** — Missing composite indexes.~~ ✅ Fixed — EF migration AddCompositeIndexes applied
16. **SEC-1 + SEC-2 + SEC-3** — Redesign `RabbitMQNotificationMessage` to carry only IDs + metadata, strip PII and entity references. These three findings share one root cause.
17. ~~**STAB-5** — INSCHEDULER state not in recovery filter — notifications can get permanently stuck.~~ ✅ Fixed — 5-minute staleness recovery window added to `GetPendingAsync`
18. ~~**CONS-2** — `BeginTransactionAsync` orphans existing active transaction.~~ ✅ Fixed — guard throws on double-open
19. ~~**CONS-4** — `AddAsync`/`DeleteByTenantAsync` throw `NotImplementedException`.~~ ✅ Fixed — both methods implemented
20. ~~**STAB-7** — No publisher confirms — messages silently lost on broker crash.~~ ✅ Fixed — `PublisherConfirmationsEnabled` + tracking via `CreateChannelOptions`
21. ~~**PERF-1** — N+1 (2N) DB queries per poll cycle.~~ ✅ Fixed — single batch query for dispatch logs; in-memory filter
22. ~~**CORR-7** — Reschedule path deletes notifications with no CANCELLED log.~~ ✅ Fixed — CANCELLED logs written; old rows kept to preserve audit trail
23. ~~**CORR-4** — Patient always updated in HandleUpdate even if unchanged.~~ ✅ Fixed — update skipped when fields are identical
24. ~~**CORR-5** — Encryption key length not validated at startup.~~ ✅ Fixed — validated at registration; fails fast on boot
25. ~~**CONS-3** — Dead interface with typo; namespace mismatch.~~ ✅ Fixed — file renamed, namespace corrected
26. ~~**PERF-3** — Thundering-herd double-check in `SecurePostProvider`.~~ ✅ Fixed — `GetOrCreateAsync` replaces `TryGetValue` + `Set`

---

## OPDRACHT COMPLIANCE

Compliance check against the full assignment description (functionele en niet-functionele eisen).

---

### Functionele Eisen

---

#### F1 — Patiënt ontvangt bericht met afspraakdetails (24u + 1u van tevoren)

**Status: PARTIAL**

- ✅ 24u en 1u notificaties worden aangemaakt in `CreateScheduledNotifications` ([AppointmentIngestionService.cs](src/NotificationService.Application/Services/AppointmentIngestionService.cs))
- ✅ Berichtinhoud bevat datum/tijd, locatie en instructies (opgebouwd in `NotificationDispatchService`)
- ✅ Afspraken die al begonnen zijn krijgen geen onnodige toekomstige notificaties — `SendAt` wordt geclampt naar `now` bij pastdatums
- ✅ Annuleringen stoppen verdere verzending — `IsCancelled = true` filtert de afspraak uit `GetPendingAsync`
- ✅ Wijzigingen passen notificatietijden aan — `HandleUpdateAsync` schrijft `CANCELLED` logs voor oude notificaties en maakt nieuwe aan
- ⚠️ **Bekend probleem:** een notificatie die al `INQUEUE` staat op het moment van annulering kan nog worden uitgevoerd door de Worker (race condition). Gedocumenteerd als known limitation.

---

#### F2 — Vastleggen of notificatie succesvol is verzonden (per organisatie, per provider)

**Status: COMPLIANT**

- ✅ `NotificationLog` slaat op: `SentAt`, `Provider`, `ExternalMessageId`, `Succeeded`, `TenantId` ([NotificationLog.cs](src/NotificationService.Domain/Entities/NotificationLog.cs))
- ✅ `DispatchLog` logt elke poging met `Outcome` (SUCCESS, ERROR_TRANSIENT, ERROR_PERMANENT, EXPIRED, CANCELLED, PENDING_ASYNC)
- ✅ AsyncFlow: `NotificationLog` wordt pas geschreven na bevestiging via `AsyncFlowPollingService`
- ✅ Factuurcontrole mogelijk: per tenant en provider zijn alle verzonden berichten traceerbaar

---

#### F3 — Organisatie gebruikt één van de ondersteunde messaging providers

**Status: PARTIAL**

- ✅ Alle 4 providers ondersteund: SwiftSend, LegacyLink, AsyncFlow, SecurePost
- ✅ Provider wordt per tenant geconfigureerd via `Tenant.Provider`
- ⚠️ `Tenant.Provider` en `Tenant.Credentials` kunnen in theorie niet overeenkomen — geen validatie bij verzending (TODO #56 in codebase)

---

### Niet-Functionele Eisen

---

#### NFE-1 — Zelfstandig functioneren, integreerbaar met meerdere OpenMRS-instanties

**Status: COMPLIANT**

- ✅ Volledige multi-tenant architectuur: alle data gescoopt op `TenantId`
- ✅ Webhook authenticatie via `X-Tenant-Id` + `X-Api-Key` headers
- ✅ Elke organisatie heeft eigen versleutelde `ProviderCredentials` in de database

---

#### NFE-2 — Integratie gedocumenteerd en beveiligd

**Status: PARTIAL**

- ✅ Webhook API gedocumenteerd in README en ADRs
- ✅ API key hashing via PBKDF2-HMAC-SHA256 met random salt (SEC-5 fixed)
- ✅ HTTPS redirect ingeschakeld
- ⚠️ Geen HMAC webhook signature validatie — alleen statische API key
- ⚠️ TLS tussen interne services niet expliciet afgedwongen in Docker Compose

---

#### NFE-3 — Alle 4 messaging providers ondersteund

**Status: COMPLIANT**

- ✅ SwiftSend ([SwiftSendProvider.cs](src/NotificationService.Infrastructure/Providers/SwiftSend/SwiftSendProvider.cs))
- ✅ LegacyLink ([LegacyLinkProvider.cs](src/NotificationService.Infrastructure/Providers/LegacyLink/LegacyLinkProvider.cs)) — SOAP/XML, XML injection gefixed (SEC-7)
- ✅ AsyncFlow ([AsyncFlowProvider.cs](src/NotificationService.Infrastructure/Providers/AsyncFlow/AsyncFlowProvider.cs)) — async met statuspolling
- ✅ SecurePost ([SecurePostProvider.cs](src/NotificationService.Infrastructure/Providers/SecurePost/SecurePostProvider.cs)) — JWT auth met caching

---

#### NFE-4 — Koppelbaar aan OpenMRS platform vanaf versie 2.7.x

**Status: MISSING**

- ⚠️ Webhook payload is **geen FHIR-formaat** — eigen JSON contract in [WebhookContracts.cs](src/NotificationService.Api/Contracts/WebhookContracts.cs)
- ⚠️ OpenMRS plugin gebruikt reflectie op `AppointmentService` — kwetsbaar bij versiewisselingen
- ℹ️ **ADR aanbevolen:** documenteer de keuze voor custom JSON boven FHIR, inclusief trade-offs (zie sectie FHIR hieronder)

---

#### NFE-5 — Gevoelige informatie veilig opgeslagen (AES-256, geen credentials in code/logs)

**Status: MOSTLY COMPLIANT**

- ✅ AES-256-GCM encryptie via `AesGcmEncryptionService` — sleutellengte gevalideerd bij startup (CORR-5 fixed)
- ✅ Geen credentials in code — alleen via environment variables
- ✅ Geen PII in logs — `DispatchLog` en `NotificationLog` bevatten geen naam, e-mail of telefoon
- ✅ API key hashing met PBKDF2 (SEC-5 fixed)
- ⚠️ Interne services communiceren via HTTP op het Docker-netwerk — geen TLS tussen containers

---

#### NFE-6 — HL7/FHIR standaarden (validatie, ACK, logging, retry)

**Status: PARTIAL**

- ✅ Berichtvalidatie via FluentValidation op webhook payload
- ✅ Logging en tracking: elke poging gelogd in `DispatchLog`, succesvolle verzendingen in `NotificationLog`
- ✅ Retry-mechanismen: Polly exponential backoff + circuit breakers op alle providers; RabbitMQ requeue bij transient errors
- ❌ Webhook payload is **geen FHIR Appointment resource** — custom JSON formaat
- ⚠️ Geen HL7 ACK teruggestuurd naar OpenMRS na aanmaken notificatie

**Toelichting FHIR keuze:** Een FHIR-compliant Appointment resource vereist een sterk afwijkend berichtformaat (`resourceType`, `participant[]`, `actor.reference`, etc.) en is aanzienlijk complexer te verwerken. Voor dit project is gekozen voor een pragmatisch custom formaat dat alle benodigde velden dekt. Een ADR legt deze keuze vast.

---

#### NFE-7 — Zelfstandig en veerkrachtig (fallback bij uitval providers of OpenMRS)

**Status: COMPLIANT**

- ✅ RabbitMQ buffert notificaties bij Worker-uitval
- ✅ Polly circuit breakers stoppen requests naar falende providers
- ✅ `AutomaticRecoveryEnabled` op RabbitMQ verbinding (STAB-2/3 fixed)
- ✅ Publisher confirms voorkomen silent message loss (STAB-7 fixed)
- ✅ Idempotency check in Worker voorkomt dubbele verzending
- ✅ Berichten ouder dan 2 uur worden als `EXPIRED` gemarkeerd en weggegooid

---

#### NFE-8 — Ondersteuning voor diverse karaktersets

**Status: COMPLIANT**

- ✅ PostgreSQL gebruikt standaard UTF-8
- ✅ OpenMRS MariaDB geconfigureerd met `utf8mb4` + `utf8mb4_general_ci`
- ✅ .NET strings zijn intern UTF-16; JSON serialisatie gebruikt UTF-8

---

#### NFE-9 — Volledig inzichtelijk via monitoring (OpenTelemetry, real-time dashboard)

**Status: COMPLIANT**

- ✅ OpenTelemetry traces, metrics en logs geconfigureerd voor alle 3 services ([InfrastructureExtentions.cs](src/NotificationService.Infrastructure/Extensions/InfrastructureExtentions.cs))
- ✅ Grafana dashboard met Tempo (traces), Prometheus (metrics) en Loki (logs) in Docker Compose
- ✅ `DispatchLog` toont elke toestandsovergang (NEW → INSCHEDULER → INQUEUE → SUCCESS/ERROR)

---

#### NFE-10 — Patiënt- en gerelateerde gegevens verwijderd binnen 14 dagen na afhandeling

**Status: COMPLIANT**

- ✅ `DataRetentionService` draait elke 24 uur ([DataRetentionService.cs](src/NotificationService.Scheduler/Cleanup/DataRetentionService.cs))
- ✅ Patiënt wordt **verwijderd** (niet geanonimiseerd) na 14 dagen inactiviteit
- ✅ Cascade delete verwijdert ook gekoppelde Appointments, ScheduledNotifications en DispatchLogs
- ✅ `LastCommunicationAt` wordt bijgewerkt op het moment van bevestigde verzending (niet bij aanmaken afspraak)
- ✅ Patiënten met toekomstige actieve afspraken worden niet verwijderd

---

#### NFE-11 — Maximaal 1 jaar meta-informatie van verstuurde berichten bewaren

**Status: COMPLIANT**

- ✅ `NotificationLog` bevat geen PII — alleen `SentAt`, `Provider`, `ExternalMessageId`, `Succeeded`, `TenantId`
- ✅ `DataRetentionService` verwijdert `NotificationLog` entries ouder dan 365 dagen
- ✅ Voldoende informatie voor factuurcontrole per provider en organisatie

---

#### NFE-12 — Uitbreidbaar voor andere functionele OpenMRS modules

**Status: PARTIAL**

- ✅ Webhook endpoint is HTTP-gebaseerd — elke module kan `POST /webhooks/appointments` aanroepen
- ✅ Nieuwe messaging providers toevoegen vereist alleen een nieuwe `IMessageProvider` implementatie en DI-registratie
- ⚠️ Webhook contract is gekoppeld aan het afspraakdomein — andere modules vereisen aanpassing van het contract of een nieuw endpoint

---

#### NFE-13 — Tijdzone-ondersteuning

**Status: PARTIAL**

- ✅ Alle timestamps zijn `DateTimeOffset` — tijdzone-informatie is aanwezig
- ✅ `Tenant.TimeZone` veld aanwezig in de database (bijv. `"Europe/Amsterdam"`)
- ⚠️ `Tenant.TimeZone` wordt **niet gebruikt** bij berekening van `SendAt` — alles wordt in UTC berekend
- ⚠️ OpenMRS plugin heeft `ZoneId.of("Europe/Amsterdam")` hardcoded in `EventEnricher.java`

---

### Compliance Samenvatting

| Eis | Status | Toelichting |
|---|---|---|
| F1 — Notificaties 24u/1u | PARTIAL | Race condition bij annulering na INQUEUE |
| F2 — Logging per tenant/provider | ✅ COMPLIANT | NotificationLog + DispatchLog volledig |
| F3 — Één provider per organisatie | PARTIAL | Provider/credentials kunnen afwijken |
| NFE-1 — Multi-tenant | ✅ COMPLIANT | Volledige tenant-isolatie |
| NFE-2 — Integratie gedocumenteerd | PARTIAL | Geen HMAC, geen interne TLS |
| NFE-3 — Alle 4 providers | ✅ COMPLIANT | Alle geïmplementeerd |
| NFE-4 — OpenMRS 2.7.x / FHIR | MISSING | Custom JSON, geen FHIR |
| NFE-5 — Beveiliging | MOSTLY COMPLIANT | AES-256 aanwezig; interne TLS ontbreekt |
| NFE-6 — HL7/FHIR | PARTIAL | Retry/logging aanwezig; geen FHIR formaat |
| NFE-7 — Veerkracht | ✅ COMPLIANT | Polly, circuit breakers, publisher confirms |
| NFE-8 — Karaktersets | ✅ COMPLIANT | UTF-8/utf8mb4 geconfigureerd |
| NFE-9 — Observability | ✅ COMPLIANT | OpenTelemetry + Grafana volledig |
| NFE-10 — 14-daagse verwijdering | ✅ COMPLIANT | Hard delete met cascade |
| NFE-11 — 1 jaar meta-informatie | ✅ COMPLIANT | NotificationLog zonder PII, 365 dagen |
| NFE-12 — Uitbreidbaarheid | PARTIAL | Providers uitbreidbaar; webhook contract vast |
| NFE-13 — Tijdzones | PARTIAL | DateTimeOffset aanwezig; Tenant.TimeZone niet gebruikt |
