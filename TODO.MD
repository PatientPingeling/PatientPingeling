# Todo

## My stuff

5. **Consider adding `TenantId` to `ScheduledNotification`** — currently the Scheduler resolves TenantId via `ScheduledNotification → Appointment.TenantId` (join). Denormalizing it onto `ScheduledNotification` would simplify the Scheduler query. Requires a new migration.

4. **Resolve `Tenant.Provider` vs `Tenant.Credentials` conflict** — `Tenant` has both a `Provider` string AND `ICollection<ProviderCredential>`. These can disagree. Decide which one drives provider selection and remove the other.



1. **Verify `.env` file exists** with all variables referenced in docker-compose:
   - `POSTGRES_HOST`, `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`
   - `RABBITMQ_HOST`, `RABBITMQ_USERNAME`, `RABBITMQ_PASSWORD`
   - `ASPNETCORE_ENVIRONMENT`

2. **Delete or fill `DTOs/`** — currently an empty folder in `NotificationService.Application`

3. **Implement repository bodies** — stubs are in place (`throw new NotImplementedException()`), real logic still needed for:
   - `AppointmentRepository.IngestAsync`
   - `PatientRepository.GetByIdAsync`
   - `TenantRepository.GetByIdAsync`
   - `ProviderCredentialRepository.GetByTenantAsync`
   - `ScheduledNotificationRepository` — all three methods, most critical for scheduler
   - `NotificationLogRepository.AddAsync`

## Wouter's PR (#28) — needs fixes before merge

4. **🔴 Remove hardcoded credentials** (`admin` / `Admin123`):
   - `config.xml` — clear the `<value>` tags, leave empty
   - `SimpleAppointmentListener.java` — replace `Context.authenticate("admin", "Admin123")` with a lookup from OpenMRS global properties (`patientpingeling.serviceuser` / `patientpingeling.servicepassword`)

5. **🔴 Remove patient data from logs** — the reflective getter dump in `SimpleAppointmentListener` logs all appointment fields (patient name, DOB, clinical data). Delete the loop, replace with a single `log.info("Enriched appointment uuid=" + uuid)`.

6. **🟡 Fix log levels in `EventSubscriber` and `PatientpingelingenricherActivator`** — normal lifecycle events are logged at `error`. Switch happy-path to `info`/`debug`, keep `error` for actual failures only.
