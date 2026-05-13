package org.openmrs.module.patientpingeling.enricher.event;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.api.context.Context;

import java.lang.reflect.Method;

public class EventEnricher {
	
	private static final Log log = LogFactory.getLog(EventEnricher.class);
	
	public Object enrichAppointment(String appointmentUuid) {
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
			
			log.info("PP_ENRICHER: Successfully enriched appointment: " + appointmentUuid);
			return appointment;
			
		}
		catch (Exception e) {
			log.error("PP_ENRICHER: Error enriching appointment " + appointmentUuid, e);
			return null;
		}
	}
}
