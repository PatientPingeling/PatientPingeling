package org.openmrs.module.patientpingeling.enricher.event;

import org.openmrs.module.patientpingeling.enricher.Model.EnrichedEvent;
import org.openmrs.module.patientpingeling.enricher.PatientpingelingenricherActivator; // Import toegevoegd
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.api.context.Context;
import org.openmrs.api.context.Daemon;

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
			    public Object invoke(Object proxy, final Method method, final Object[] args) {
				    if ("onMessage".equals(method.getName()) && args != null && args.length == 1) {
					    try {
						    final Object message = args[0];
						    if (mapMessageClass.isInstance(message)) {
							    final Method getString = mapMessageClass.getMethod("getString", String.class);
							    
							    final String action = (String) getString.invoke(message, "action");
							    final String uuid = (String) getString.invoke(message, "uuid");
							    
							    log.error("PP_EVENT_LOG: Received " + action + " for UUID: " + uuid);
							    
							    // Daemon aanroep met het token van de Activator
							    Daemon.runInDaemonThread(new Runnable() {
								    
								    @Override
								    public void run() {
									    try {
										    Context.openSession();
										    EventEnricher enricher = new EventEnricher();
										    EnrichedEvent enriched = enricher.enrichAppointment(uuid, action);
										    
										    if (enriched == null) {
											    log.error("PP_ENRICHER: Enrichment returned null for UUID: " + uuid);
										    }else{
											// Logica for webhook benaderen met enriched data.
											}
									    }
									    catch (Exception e) {
										    log.error("PP_ENRICHER: Error in background processing", e);
									    }
									    finally {
										    Context.closeSession();
									    }
								    }
							    }, PatientpingelingenricherActivator.getDaemonToken());
						    }
					    }
					    catch (Exception e) {
						    log.error("PP_ENRICHER: Error during proxy invocation", e);
					    }
				    }
				    return null;
			    }
		    });
	}
}
