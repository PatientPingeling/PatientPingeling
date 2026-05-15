#!/bin/bash
docker exec patientpingeling-postgres-1 psql -U postgres -d notificationservice -c \
  'TRUNCATE "Appointments", "Patients", "ScheduledNotifications" RESTART IDENTITY CASCADE;'
echo "Dev DB reset complete."
