package org.openmrs.module.patientpingeling.enricher.event;

import org.openmrs.module.patientpingeling.enricher.Model.EnrichedEvent;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.api.context.Context;

import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;

public class SimpleAppointmentListener {
	
	private static final Log log = LogFactory.getLog(SimpleAppointmentListener.class);
	
	public static Object createProxy() throws Exception {
		Class<?> eventListenerClass = Context.loadClass("org.openmrs.event.EventListener");
		final Class<?> mapMessageClass = eventListenerClass.getClassLoader().loadClass("javax.jms.MapMessage");
		
		return Proxy.newProxyInstance(eventListenerClass.getClassLoader(), new Class<?>[] { eventListenerClass },
		    new InvocationHandler() {
			    
			    @Override
			    public Object invoke(Object proxy, Method method, Object[] args) {
				    if ("onMessage".equals(method.getName()) && args != null && args.length == 1) {
					    try {
						    Object message = args[0];
						    if (mapMessageClass.isInstance(message)) {
							    Method getString = mapMessageClass.getMethod("getString", String.class);
							    
							    // 'action' bevat CREATED, UPDATED, etc.
							    String action = (String) getString.invoke(message, "action");
							    String uuid = (String) getString.invoke(message, "uuid");
							    
							    log.error("PP_EVENT_LOG: Received " + action + " for UUID: " + uuid);
							    
							    Context.openSession();
							    try {
								    Context.authenticate("admin", "Admin123");
								    EventEnricher enricher = new EventEnricher();
								    
								    // Geef nu zowel UUID als ACTION mee aan de enricher
								    EnrichedEvent enriched = (EnrichedEvent) enricher.enrichAppointment(uuid, action);
								    
								    if (enriched == null) {
									    log.error("PP_ENRICHER: Enrichment returned null for UUID: " + uuid);
								    }
								    // De succesvolle log wordt nu in EventEnricher gedaan, 
								    // dus die hoeft hier niet dubbel te staan.
							    }
							    finally {
								    Context.closeSession();
							    }
						    }
					    }
					    catch (Exception e) {
						    log.error("PP_ENRICHER: Error during message processing", e);
					    }
				    }
				    return null;
			    }
		    });
	}
}
