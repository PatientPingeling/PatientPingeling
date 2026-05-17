#!/bin/bash
docker exec patientpingeling-postgres-1 psql -U postgres -d notificationservice -c \
  'TRUNCATE "Tenants", "Appointments", "Patients", "ScheduledNotifications", "NotificationLogs", "ProviderCredentials" RESTART IDENTITY CASCADE;'
echo "Dev DB reset complete — all tables nuked."
