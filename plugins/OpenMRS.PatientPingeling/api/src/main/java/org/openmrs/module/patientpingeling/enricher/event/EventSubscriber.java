package org.openmrs.module.patientpingeling.enricher.event;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.api.context.Context;

public class EventSubscriber {
	
	private static final Log log = LogFactory.getLog(EventSubscriber.class);
	
	private static Object listenerInstance = null;
	
	public static void subscribe() {
		log.error("PP_ENRICHER: EventSubscriber.subscribe() called");
		try {
			log.error("PP_ENRICHER: Loading Appointment class...");
			Class<?> appointmentClass = Context.loadClass("org.openmrs.module.appointments.model.Appointment");
			log.error("PP_ENRICHER: Appointment class loaded: " + appointmentClass);
			
			log.error("PP_ENRICHER: Creating proxy listener...");
			listenerInstance = SimpleAppointmentListener.createProxy();
			log.error("PP_ENRICHER: Proxy created: " + listenerInstance);
			
			log.error("PP_ENRICHER: Loading Event class...");
			Class<?> eventClass = Context.loadClass("org.openmrs.event.Event");
			log.error("PP_ENRICHER: Event class loaded: " + eventClass);
			
			log.error("PP_ENRICHER: Loading EventListener class...");
			Class<?> eventListenerClass = Context.loadClass("org.openmrs.event.EventListener");
			log.error("PP_ENRICHER: EventListener class loaded: " + eventListenerClass);
			
			log.error("PP_ENRICHER: Calling Event.subscribe()...");
			eventClass.getMethod("subscribe", Class.class, String.class, eventListenerClass).invoke(null, appointmentClass,
			    null, listenerInstance);
			log.error("PP_ENRICHER: Event.subscribe() completed successfully.");
			
		}
		catch (Exception e) {
			log.error("PP_ENRICHER: subscribe() failed at: " + e.getMessage(), e);
		}
	}
	
	public static void unsubscribe() {
		log.error("PP_ENRICHER: EventSubscriber.unsubscribe() called");
		try {
			if (listenerInstance == null) {
				log.warn("PP_ENRICHER: listenerInstance is null, nothing to unsubscribe.");
				return;
			}
			
			Class<?> appointmentClass = Context.loadClass("org.openmrs.module.appointments.model.Appointment");
			Class<?> eventClass = Context.loadClass("org.openmrs.event.Event");
			Class<?> eventListenerClass = Context.loadClass("org.openmrs.event.EventListener");
			
			eventClass.getMethod("unsubscribe", Class.class, String.class, eventListenerClass).invoke(null,
			    appointmentClass, null, listenerInstance);
			log.error("PP_ENRICHER: Unsubscribe completed.");
			
		}
		catch (Exception e) {
			log.error("PP_ENRICHER: unsubscribe() failed: " + e.getMessage(), e);
		}
	}
}
