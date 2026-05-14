package org.openmrs.module.patientpingeling.enricher.event;

import org.openmrs.module.patientpingeling.enricher.Model.EnrichedEvent;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.api.context.Context;
import org.codehaus.jackson.map.ObjectMapper;

import java.io.IOException;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;

public class SimpleAppointmentListener {
	
	private static final Log log = LogFactory.getLog(SimpleAppointmentListener.class);
	
	private static final String API_KEY = System.getenv("PP_API_KEY");
	
	private static final String WEBHOOK_URL = System.getenv("PP_WEBHOOK_URL");
	
	private static final String TENANT_ID = System.getenv("PP_TENANT_KEY");
	
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
							    
							    Thread t = new Thread(new Runnable() {
								    
								    @Override
								    public void run() {
									    try {
										    Context.openSession();
										    Context.authenticate("admin", "Admin123");
										    
										    EventEnricher enricher = new EventEnricher();
										    EnrichedEvent enriched = enricher.enrichAppointment(uuid, action);
										    
										    if (enriched != null) {
											    ObjectMapper mapper = new ObjectMapper();
											    String json = mapper.writeValueAsString(enriched);
											    log.error("PP_DEBUG: JSON built, calling webhook...");
											    webHookCaller(json);
										    }
									    }
									    catch (Exception e) {
										    log.error("PP_FATAL: Thread crashed", e);
									    }
									    finally {
										    Context.closeSession();
									    }
								    }
							    });
							    t.start();
						    }
					    }
					    catch (Exception e) {
						    log.error("PP_ENRICHER: Proxy error", e);
					    }
				    }
				    return null;
			    }
		    });
	}
	
	private static void webHookCaller(String jsonPayload) {
		log.error("PP_WEBHOOK: Starting caller. URL is: " + WEBHOOK_URL);
		int maxRetries = 3;
		int waitTime = 10000;
		
		for (int i = 1; i <= maxRetries; i++) {
			HttpURLConnection conn = null;
			OutputStream os = null;
			try {
				log.error("PP_WEBHOOK: Attempt " + i + " to send data...");
				
				URL url = new URL(WEBHOOK_URL);
				conn = (HttpURLConnection) url.openConnection();
				conn.setRequestMethod("POST");
				conn.setRequestProperty("Content-Type", "application/json; utf-8");
				conn.setRequestProperty("X-Api-Key", API_KEY);
				conn.setRequestProperty("X-Tenant-Id", TENANT_ID);
				conn.setDoOutput(true);
				conn.setConnectTimeout(5000);
				
				os = conn.getOutputStream();
				byte[] input = jsonPayload.getBytes(StandardCharsets.UTF_8);
				os.write(input, 0, input.length);
				os.flush();
				os.close();
				os = null;
				
				int code = conn.getResponseCode();
				if (code >= 200 && code < 300) {
					log.error("PP_WEBHOOK: Success! Status code: " + code);
					return;
				} else {
					throw new Exception("HTTP error code: " + code);
				}
			}
			catch (Exception e) {
				log.error("PP_WEBHOOK: Attempt " + i + " failed: " + e.getMessage());
				
				if (i < maxRetries) {
					try {
						log.error("PP_WEBHOOK: Waiting 10 seconds before retry...");
						Thread.sleep(waitTime);
					}
					catch (InterruptedException ie) {
						Thread.currentThread().interrupt();
						return;
					}
				} else {
					log.error("PP_WEBHOOK: Max retries reached. Giving up.");
				}
			}
			finally {
				if (os != null) {
					try {
						os.close();
					}
					catch (IOException e) { /* Ignore */}
				}
				if (conn != null) {
					conn.disconnect();
				}
			}
		}
	}
}
