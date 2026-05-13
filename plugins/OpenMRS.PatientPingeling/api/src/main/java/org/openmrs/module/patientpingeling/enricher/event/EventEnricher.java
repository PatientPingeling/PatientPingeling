package org.openmrs.module.patientpingeling.enricher.event;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.Patient;
import org.openmrs.api.context.Context;
import org.openmrs.module.patientpingeling.enricher.Model.EnrichedEvent;
import org.openmrs.module.patientpingeling.enricher.Model.EnrichedEvent.AppointmentAction;

import java.lang.reflect.Method;
import java.time.LocalDateTime;
import java.time.ZoneId;
import java.util.Date;

public class EventEnricher {
	
	private static final Log log = LogFactory.getLog(EventEnricher.class);
	
	// Nu met eventType als parameter!
	public EnrichedEvent enrichAppointment(String appointmentUuid, String eventType) {
		if (appointmentUuid == null || appointmentUuid.isEmpty()) {
			log.error("PP_ENRICHER: Appointment UUID must not be null or empty");
			return null;
		}
		
		try {
			Class<?> appointmentServiceClass = Context
			        .loadClass("org.openmrs.module.appointments.service.AppointmentsService");
			Object appointmentService = Context.getService((Class) appointmentServiceClass);
			Method getAppointmentByUuid = appointmentServiceClass.getMethod("getAppointmentByUuid", String.class);
			Object appointment = getAppointmentByUuid.invoke(appointmentService, appointmentUuid);
			
			if (appointment == null) {
				log.error("PP_ENRICHER: No appointment found for UUID: " + appointmentUuid);
				return null;
			}
			
			EnrichedEvent enriched = new EnrichedEvent();
			enriched.setUuid(appointmentUuid);
			
			// 1. Status & Action mapping
			String rawStatus = invokeStringMethod(appointment, "getStatus");
			enriched.setStatus(rawStatus);
			
			if ("Cancelled".equalsIgnoreCase(rawStatus)) {
				enriched.setAction(AppointmentAction.CANCELLED);
			} else if ("UPDATED".equals(eventType)) {
				enriched.setAction(AppointmentAction.UPDATED);
			} else if ("CREATED".equals(eventType)) {
				enriched.setAction(AppointmentAction.CREATED);
			} else {
				enriched.setAction(AppointmentAction.UNKNOWN);
			}
			
			// 2. Service/Department Extraction (Multiple fallbacks)
			String serviceName = "Unknown Service";
			try {
				// Poging A: getService()
				Object sObj = null;
				try {
					sObj = appointment.getClass().getMethod("getService").invoke(appointment);
				}
				catch (Exception ignored) {}
				
				// Poging B: getAppointmentService()
				if (sObj == null) {
					try {
						sObj = appointment.getClass().getMethod("getAppointmentService").invoke(appointment);
					}
					catch (Exception ignored) {}
				}
				
				// Poging C: getAppointmentType()
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
				log.warn("PP_ENRICHER: Could not extract service name");
			}
			enriched.setAppointmentService(serviceName);
			
			// 3. General Data
			enriched.setLocatie(invokeStringMethod(appointment, "getLocation"));
			enriched.setComments(invokeStringMethod(appointment, "getComments"));
			
			Date startDate = (Date) appointment.getClass().getMethod("getStartDateTime").invoke(appointment);
			if (startDate != null) {
				enriched.setDatumEnTijd(startDate.toInstant().atZone(ZoneId.systemDefault()).toLocalDateTime());
			}
			
			// 4. Patient Details
			Patient patient = (Patient) appointment.getClass().getMethod("getPatient").invoke(appointment);
			if (patient != null) {
				enriched.setNaam(patient.getPersonName().getFullName());
				enriched.setEmail(patient.getAttribute("email") != null ? patient.getAttribute("email").getValue() : "N/A");
				enriched.setTel(patient.getAttribute("Telephone Number") != null ? patient.getAttribute("Telephone Number")
				        .getValue() : "N/A");
			}
			
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
