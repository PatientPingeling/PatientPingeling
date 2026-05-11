/**
 * This Source Code Form is subject to the terms of the Mozilla Public License,
 * v. 2.0. If a copy of the MPL was not distributed with this file, You can
 * obtain one at http://mozilla.org/MPL/2.0/. OpenMRS is also distributed under
 * the terms of the Healthcare Disclaimer located at http://openmrs.org/license.
 *
 * Copyright (C) OpenMRS Inc. OpenMRS is a registered trademark and the OpenMRS
 * graphic logo is a trademark of OpenMRS Inc.
 */
package org.openmrs.module.patientpingeling.enricher;

import java.lang.reflect.Method;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Proxy;
import java.util.Timer;
import java.util.TimerTask;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.OpenmrsObject;
import org.openmrs.module.BaseModuleActivator;
import org.openmrs.module.ModuleException;
import org.openmrs.module.ModuleFactory;
import org.openmrs.module.patientpingeling.enricher.event.EventModuleEventLoggingListener;

/**
 * This class contains the logic that is run every time this module is either started or shutdown
 */
public class PatientpingelingenricherActivator extends BaseModuleActivator {
	
	private Log log = LogFactory.getLog(this.getClass());
	
	private final EventModuleEventLoggingListener eventLoggingListener = new EventModuleEventLoggingListener();
	
	private Object eventModuleListenerProxy;
	
	private Timer subscribeTimer;
	
	private int subscribeAttempts = 0;
	
	private boolean eventModuleUnavailableLogged = false;
	
	private static final int MAX_SUBSCRIBE_ATTEMPTS = 12; // ~1 minute at 5s interval
	
	private static final long SUBSCRIBE_RETRY_INTERVAL_MS = 5000L;
	
	/**
	 * @see #started()
	 */
	public void started() {
		log.info("Started Patientpingeling enricher (Event Module logging enabled)");
		
		startSubscriptionRetryLoop();
	}
	
	/**
	 * @see #shutdown()
	 */
	public void shutdown() {
		stopSubscriptionRetryLoop();
		
		unsubscribeFromEventModuleIfAvailable();
		
		log.info("Shutdown Patientpingeling enricher");
	}
	
	private synchronized void startSubscriptionRetryLoop() {
		if (subscribeTimer != null) {
			return;
		}
		
		subscribeAttempts = 0;
		subscribeTimer = new Timer(true);
		subscribeTimer.scheduleAtFixedRate(new TimerTask() {
			
			@Override
			public void run() {
				try {
					subscribeAttempts++;
					boolean subscribed = subscribeToEventModuleIfAvailable();
					if (subscribed) {
						stopSubscriptionRetryLoop();
						return;
					}
					
					if (subscribeAttempts >= MAX_SUBSCRIBE_ATTEMPTS) {
						log.warn("PP_ENRICHER_EVENT subscribe_gave_up=true attempts=" + subscribeAttempts
						        + " (you can restart this module after Event Module is installed/started)");
						stopSubscriptionRetryLoop();
					}
				}
				catch (Exception e) {
					// Never let background subscription crash the app
					log.error("PP_ENRICHER_EVENT subscribe_background_error=true", e);
				}
			}
		}, 0L, SUBSCRIBE_RETRY_INTERVAL_MS);
	}
	
	private synchronized void stopSubscriptionRetryLoop() {
		if (subscribeTimer == null) {
			return;
		}
		try {
			subscribeTimer.cancel();
		}
		catch (Exception ignored) {
			// ignore
		}
		subscribeTimer = null;
	}
	
	private synchronized boolean subscribeToEventModuleIfAvailable() {
		if (eventModuleListenerProxy != null) {
			return true;
		}
		
		try {
			ClassLoader eventModuleClassLoader = getEventModuleClassLoader();
			if (eventModuleClassLoader == null) {
				logEventModuleUnavailableOnce();
				return false;
			}
			
			Class<?> eventClass = loadFirstClass(eventModuleClassLoader, new String[] { "org.openmrs.event.Event",
			        "org.openmrs.eventbus.Event" });
			Class<?> eventListenerInterface = loadFirstClass(eventModuleClassLoader, new String[] {
			        "org.openmrs.event.EventListener", "org.openmrs.eventbus.EventListener" });
			
			Object listenerProxy = Proxy.newProxyInstance(eventListenerInterface.getClassLoader(),
			    new Class[] { eventListenerInterface }, new InvocationHandler() {
				    
				    @Override
				    public Object invoke(Object proxy, Method method, Object[] args) throws Throwable {
					    String methodName = method.getName();
					    if ("onMessage".equals(methodName) && args != null && args.length == 1) {
						    eventLoggingListener.onMessage(args[0]);
						    return null;
					    }
					    
					    if ("toString".equals(methodName) && (args == null || args.length == 0)) {
						    return "PatientpingelingEnricherEventListenerProxy";
					    }
					    if ("hashCode".equals(methodName) && (args == null || args.length == 0)) {
						    return System.identityHashCode(proxy);
					    }
					    if ("equals".equals(methodName) && args != null && args.length == 1) {
						    return proxy == args[0];
					    }
					    
					    return null;
				    }
			    });
			
			Method subscribe = eventClass.getMethod("subscribe", Class.class, String.class, eventListenerInterface);
			// Subscribe to *all* actions for OpenmrsObject (and therefore all subclasses)
			subscribe.invoke(null, OpenmrsObject.class, null, listenerProxy);
			
			eventModuleListenerProxy = listenerProxy;
			log.info("PP_ENRICHER_EVENT subscribed_to=OpenmrsObject actions=ALL");
			eventModuleUnavailableLogged = false;
			return true;
		}
		catch (ClassNotFoundException e) {
			logEventModuleUnavailableOnce();
			return false;
		}
		catch (Exception e) {
			log.error("PP_ENRICHER_EVENT failed_to_subscribe=true", e);
			return false;
		}
	}
	
	private void logEventModuleUnavailableOnce() {
		if (eventModuleUnavailableLogged) {
			return;
		}
		eventModuleUnavailableLogged = true;
		log.warn("PP_ENRICHER_EVENT event_module_not_available=true (install/start the Event Module to enable logging)");
	}
	
	private ClassLoader getEventModuleClassLoader() {
		String[] candidateModuleIds = new String[] { "event", "org.openmrs.module.event" };
		for (int i = 0; i < candidateModuleIds.length; i++) {
			String moduleId = candidateModuleIds[i];
			try {
				if (!ModuleFactory.isModuleStarted(moduleId)) {
					continue;
				}
				return ModuleFactory.getModuleClassLoader(moduleId);
			}
			catch (ModuleException e) {
				// try next
			}
		}
		return null;
	}
	
	private Class<?> loadFirstClass(ClassLoader classLoader, String[] candidateClassNames) throws ClassNotFoundException {
		ClassNotFoundException lastException = null;
		for (int i = 0; i < candidateClassNames.length; i++) {
			String className = candidateClassNames[i];
			try {
				return Class.forName(className, true, classLoader);
			}
			catch (ClassNotFoundException e) {
				lastException = e;
			}
		}
		throw lastException == null ? new ClassNotFoundException("No candidate class found") : lastException;
	}
	
	private synchronized void unsubscribeFromEventModuleIfAvailable() {
		if (eventModuleListenerProxy == null) {
			return;
		}
		
		try {
			ClassLoader eventModuleClassLoader = getEventModuleClassLoader();
			if (eventModuleClassLoader == null) {
				return;
			}
			
			Class<?> eventClass = loadFirstClass(eventModuleClassLoader, new String[] { "org.openmrs.event.Event",
			        "org.openmrs.eventbus.Event" });
			Class<?> eventListenerInterface = loadFirstClass(eventModuleClassLoader, new String[] {
			        "org.openmrs.event.EventListener", "org.openmrs.eventbus.EventListener" });
			Class<?> actionEnumClass = loadFirstClass(eventModuleClassLoader, new String[] {
			        "org.openmrs.event.Event$Action", "org.openmrs.eventbus.Event$Action" });
			
			Method unsubscribe = eventClass.getMethod("unsubscribe", Class.class, actionEnumClass, eventListenerInterface);
			unsubscribe.invoke(null, OpenmrsObject.class, null, eventModuleListenerProxy);
			
			log.info("PP_ENRICHER_EVENT unsubscribed_from=OpenmrsObject actions=ALL");
		}
		catch (ClassNotFoundException e) {
			// ignore
		}
		catch (Exception e) {
			log.error("PP_ENRICHER_EVENT failed_to_unsubscribe=true", e);
		}
		finally {
			eventModuleListenerProxy = null;
		}
	}
	
}
