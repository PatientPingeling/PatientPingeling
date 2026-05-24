package org.openmrs.module.patientpingeling.enricher.event;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.Patient;
import org.openmrs.api.context.Context;
import org.openmrs.module.patientpingeling.enricher.Model.EnrichedEvent;
import org.openmrs.module.patientpingeling.enricher.Model.EnrichedEvent.AppointmentAction;

import java.time.format.DateTimeFormatter;
import java.lang.reflect.Method;
import java.time.ZoneOffset;
import java.util.Date;

public class EventEnricher {
	
	private static final Log log = LogFactory.getLog(EventEnricher.class);
	
	public EnrichedEvent enrichAppointment(String appointmentUuid, String eventType) {
		if (appointmentUuid == null || appointmentUuid.isEmpty()) {
			log.info("PP_ENRICHER: Appointment UUID must not be null or empty");
			return null;
		}
		
		try {
			Class<?> appointmentServiceClass = Context
			        .loadClass("org.openmrs.module.appointments.service.AppointmentsService");
			Object appointmentService = Context.getService((Class) appointmentServiceClass);
			Method getAppointmentByUuid = appointmentServiceClass.getMethod("getAppointmentByUuid", String.class);
			Object appointment = getAppointmentByUuid.invoke(appointmentService, appointmentUuid);
			
			if (appointment == null) {
				log.info("PP_ENRICHER: No appointment found for UUID: " + appointmentUuid);
				return null;
			}
			
			// 1. Action mapping
			String rawStatus = invokeStringMethod(appointment, "getStatus");
			AppointmentAction action;
			if ("Cancelled".equalsIgnoreCase(rawStatus)) {
				action = AppointmentAction.CANCELLED;
			} else if ("UPDATED".equals(eventType)) {
				action = AppointmentAction.UPDATED;
			} else if ("CREATED".equals(eventType)) {
				action = AppointmentAction.CREATED;
			} else {
				action = AppointmentAction.UNKNOWN;
			}
			
			// 2. Service name (multiple fallbacks)
			String serviceName = null;
			try {
				Object sObj = null;
				try {
					sObj = appointment.getClass().getMethod("getService").invoke(appointment);
				}
				catch (Exception ignored) {}
				
				if (sObj == null) {
					try {
						sObj = appointment.getClass().getMethod("getAppointmentService").invoke(appointment);
					}
					catch (Exception ignored) {}
				}
				
				if (sObj == null) {
					try {
						sObj = appointment.getClass().getMethod("getAppointmentType").invoke(appointment);
					}
					catch (Exception ignored) {}
				}
				
				if (sObj != null) {
					serviceName = invokeStringMethod(sObj, "getName");
				}
			}
			catch (Exception e) {
				log.debug("PP_ENRICHER: Could not extract service name");
			}
			
			// 3. Scheduled date — emit as UTC (ISO-8601 with +00:00 offset).
			// The notification service converts to the tenant's local timezone for patient-facing messages.
			Date startDate = (Date) appointment.getClass().getMethod("getStartDateTime").invoke(appointment);
			String scheduledAt = startDate != null ? startDate.toInstant().atOffset(ZoneOffset.UTC)
			        .format(DateTimeFormatter.ISO_OFFSET_DATE_TIME) : null;
			
			// 4. Location & instructions
			String location = invokeStringMethod(appointment, "getLocation");
			String instructions = invokeStringMethod(appointment, "getComments");
			
			// 5. Patient details
			Patient patient = (Patient) appointment.getClass().getMethod("getPatient").invoke(appointment);
			EnrichedEvent.PatientDto patientDto = new EnrichedEvent.PatientDto();
			if (patient != null) {
				patientDto.setExternalId(patient.getPatientIdentifier() != null ? patient.getPatientIdentifier()
				        .getIdentifier() : appointmentUuid);
				patientDto.setGivenName(patient.getPersonName() != null ? patient.getPersonName().getGivenName() : null);
				patientDto.setEmail(patient.getAttribute("email") != null ? patient.getAttribute("email").getValue() : null);
				patientDto.setPhoneNumber(patient.getAttribute("Telephone Number") != null ? patient.getAttribute(
				    "Telephone Number").getValue() : null);
			}
			
			// 6. Appointment DTO
			EnrichedEvent.AppointmentDto apptDto = new EnrichedEvent.AppointmentDto();
			apptDto.setExternalId(appointmentUuid);
			apptDto.setScheduledAt(scheduledAt);
			apptDto.setService(serviceName);
			apptDto.setLocation(location);
			apptDto.setInstructions(instructions);
			
			// 7. Build final event
			EnrichedEvent enriched = new EnrichedEvent(action, patientDto, apptDto);
			
			log.error("PP_ENRICHER: Data successfully captured -> " + enriched.toString());
			return enriched;
			
		}
		catch (Exception e) {
			log.error("PP_ENRICHER: Error enriching appointment " + appointmentUuid, e);
			return null;
		}
	}
	
	private String invokeStringMethod(Object obj, String methodName) {
		try {
			Object result = obj.getClass().getMethod(methodName).invoke(obj);
			return (result != null) ? result.toString() : null;
		}
		catch (Exception e) {
			return null;
		}
	}
}
