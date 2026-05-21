# Hoe installeer je de PatientPingeling OpenMRS Enricher module?

Het PatientPingeling notificatie systeem is afhankelijk van de speciaal ontworpen plugin: **PatientPingeling Enricher Module**. Deze OpenMRS module is het koppelstuk tussen OpenMRS en het PatientPingeling notificatie systeem. 

Om de module succesvol te installeren moeten de volgende stappen worden ondernomen (*OpenMRS versie 2.8.4*):
1. Download de laatste release van de .omod plugin, te vinden op https://github.com/PatientPingeling/PatientPingeling/releases 
2. Log in als *Administrator* op je OpenMRS systeem via de webinterface.
3. Navigeer naar *...openmrs/admin/modules/module.list*
4. Druk op 'Add or Upgrade Module'
5. Bij 'Add Module' voeg je gedownloade release van de **PatientPingeling Enricher Module** toe.
6. Als dit is gelukt, navigeer naar de advanced settings *.../openmrs/admin/maintenance/globalProps.form*
7. Vul de open PatientPingeling velden in (de API-key en tenantId worden bij uw aanschaffing van de PatientPingeling applicatie verstrekt aan u):
   1. patientpingeling.apiKey
   2. patientpingeling.servicepassword
   3. patientpingeling.serviceuser
   4. patientpingeling.tenantId
8. Uw OpenMRS systeem is nu klaar voor integratie met het **PatientPingeling Notificatie systeem**!