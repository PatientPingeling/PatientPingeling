docker exec patientpingeling-postgres-1 psql -U postgres -d notificationservice -c `
  'TRUNCATE "Appointments", "Patients", "ScheduledNotifications" RESTART IDENTITY CASCADE;'
Write-Host "Dev DB reset complete."
