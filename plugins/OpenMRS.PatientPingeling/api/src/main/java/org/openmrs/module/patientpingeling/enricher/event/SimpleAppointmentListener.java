package org.openmrs.module.patientpingeling.enricher.event;

import org.openmrs.module.patientpingeling.enricher.event.EventEnricher;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.api.context.Context;

import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import javax.jms.MapMessage;
import javax.jms.Message;

public class SimpleAppointmentListener {
	
	private static final Log log = LogFactory.getLog(SimpleAppointmentListener.class);
	
	// Returns a dynamic proxy that implements EventListener at runtime
	public static Object createProxy() throws Exception {
		Class<?> eventListenerClass = Context.loadClass("org.openmrs.event.EventListener");
		final Class<?> mapMessageClass = eventListenerClass.getClassLoader().loadClass("javax.jms.MapMessage");
		
		return Proxy.newProxyInstance(eventListenerClass.getClassLoader(), new Class<?>[] { eventListenerClass },
		    new InvocationHandler() {
			    
			    @Override
			    public Object invoke(Object proxy, Method method, Object[] args) {
				    log.error("PP_ENRICHER: invoke called, method=" + method.getName());
				    if ("onMessage".equals(method.getName()) && args != null && args.length == 1) {
					    try {
						    Object message = args[0];
						    log.error("PP_ENRICHER: message class=" + message.getClass().getName());
						    log.error("PP_ENRICHER: isInstance=" + mapMessageClass.isInstance(message));
						    if (mapMessageClass.isInstance(message)) {
							    Method getString = mapMessageClass.getMethod("getString", String.class);
							    String action = (String) getString.invoke(message, "action");
							    String uuid = (String) getString.invoke(message, "uuid");
							    log.error("PP_EVENT_LOG: " + action + " on " + uuid);
							    
							    // TODO: Perform enriching
							    Context.openSession();
							    try {
								    Context.authenticate("admin", "Admin123");
								    EventEnricher enricher = new EventEnricher();
								    Object appointment = enricher.enrichAppointment(uuid);
								    if (appointment != null) {
									    for (java.lang.reflect.Method m : appointment.getClass().getMethods()) {
										    if (m.getName().startsWith("get") && m.getParameterCount() == 0) {
											    try {
												    Object value = m.invoke(appointment);
												    log.error("PP_ENRICHER: " + m.getName() + " = " + value);
											    }
											    catch (Exception ignored) {}
										    }
									    }
								    }
							    }
							    finally {
								    Context.closeSession();
							    }
						    }
					    }
					    catch (Exception e) {
						    log.error("PP_ENRICHER: Error reading message", e);
					    }
				    }
				    return null;
			    }
		    });
	}
}
