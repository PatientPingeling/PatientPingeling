package org.openmrs.module.patientpingeling.enricher.event;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.Patient;
import org.openmrs.api.context.Context;
import org.openmrs.module.patientpingeling.enricher.model.EnrichedEvent;
import org.openmrs.module.patientpingeling.enricher.model.EnrichedEvent.AppointmentAction;

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
			Object appointment = loadAppointment(appointmentUuid);
			
			if (appointment == null) {
				log.info("PP_ENRICHER: No appointment found for UUID: " + appointmentUuid);
				return null;
			}
			
			AppointmentAction action = mapAction(appointment, eventType);
			EnrichedEvent.PatientDto patientDto = buildPatientDto(appointment, appointmentUuid);
			EnrichedEvent.AppointmentDto apptDto = buildAppointmentDto(appointment, appointmentUuid);
			EnrichedEvent enriched = new EnrichedEvent(action, patientDto, apptDto);
			
			log.error("PP_ENRICHER: Data successfully captured -> " + enriched.toString());
			return enriched;
			
		}
		catch (Exception e) {
			log.error("PP_ENRICHER: Error enriching appointment " + appointmentUuid, e);
			return null;
		}
	}
	
	private Object loadAppointment(String appointmentUuid) throws Exception {
		Class<?> appointmentServiceClass = Context.loadClass("org.openmrs.module.appointments.service.AppointmentsService");
		Object appointmentService = Context.getService((Class) appointmentServiceClass);
		Method getAppointmentByUuid = appointmentServiceClass.getMethod("getAppointmentByUuid", String.class);
		return getAppointmentByUuid.invoke(appointmentService, appointmentUuid);
	}
	
	private AppointmentAction mapAction(Object appointment, String eventType) {
		String rawStatus = invokeStringMethod(appointment, "getStatus");
		if ("Cancelled".equalsIgnoreCase(rawStatus)) {
			return AppointmentAction.CANCELLED;
		}
		if ("UPDATED".equals(eventType)) {
			return AppointmentAction.UPDATED;
		}
		if ("CREATED".equals(eventType)) {
			return AppointmentAction.CREATED;
		}
		return AppointmentAction.UNKNOWN;
	}
	
	private EnrichedEvent.PatientDto buildPatientDto(Object appointment, String appointmentUuid) throws Exception {
		Patient patient = (Patient) appointment.getClass().getMethod("getPatient").invoke(appointment);
		EnrichedEvent.PatientDto patientDto = new EnrichedEvent.PatientDto();
		if (patient == null) {
			return patientDto;
		}
		
		patientDto.setExternalId(patient.getPatientIdentifier() != null ? patient.getPatientIdentifier().getIdentifier()
		        : appointmentUuid);
		patientDto.setGivenName(patient.getPersonName() != null ? patient.getPersonName().getGivenName() : null);
		patientDto.setEmail(patient.getAttribute("email") != null ? patient.getAttribute("email").getValue() : null);
		patientDto.setPhoneNumber(patient.getAttribute("Telephone Number") != null ? patient
		        .getAttribute("Telephone Number").getValue() : null);
		return patientDto;
	}
	
	private EnrichedEvent.AppointmentDto buildAppointmentDto(Object appointment, String appointmentUuid) throws Exception {
		EnrichedEvent.AppointmentDto apptDto = new EnrichedEvent.AppointmentDto();
		apptDto.setExternalId(appointmentUuid);
		apptDto.setScheduledAt(formatScheduledAt(appointment));
		apptDto.setService(extractServiceName(appointment));
		apptDto.setLocation(invokeStringMethod(appointment, "getLocation"));
		apptDto.setInstructions(invokeStringMethod(appointment, "getComments"));
		return apptDto;
	}
	
	private String formatScheduledAt(Object appointment) throws Exception {
		Date startDate = (Date) appointment.getClass().getMethod("getStartDateTime").invoke(appointment);
		if (startDate == null) {
			return null;
		}
		return startDate.toInstant().atOffset(ZoneOffset.UTC).format(DateTimeFormatter.ISO_OFFSET_DATE_TIME);
	}
	
	private String extractServiceName(Object appointment) {
		Object service = firstAvailableMethodResult(appointment, "getService", "getAppointmentService", "getAppointmentType");
		if (service == null) {
			log.debug("PP_ENRICHER: Could not extract service name");
			return null;
		}
		return invokeStringMethod(service, "getName");
	}
	
	private Object firstAvailableMethodResult(Object obj, String... methodNames) {
		for (String methodName : methodNames) {
			Object result = invokeMethod(obj, methodName);
			if (result != null) {
				return result;
			}
		}
		return null;
	}
	
	private Object invokeMethod(Object obj, String methodName) {
		try {
			return obj.getClass().getMethod(methodName).invoke(obj);
		}
		catch (Exception e) {
			return null;
		}
	}
	
	private String invokeStringMethod(Object obj, String methodName) {
		Object result = invokeMethod(obj, methodName);
		return (result != null) ? result.toString() : null;
	}
}
