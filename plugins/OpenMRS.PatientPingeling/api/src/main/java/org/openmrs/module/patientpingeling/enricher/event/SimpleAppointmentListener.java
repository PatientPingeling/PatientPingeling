package org.openmrs.module.patientpingeling.enricher.event;

import org.openmrs.module.patientpingeling.enricher.Model.EnrichedEvent;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.api.context.Context;
import org.openmrs.util.OpenmrsUtil;
import org.codehaus.jackson.map.ObjectMapper;

import java.io.File;
import java.io.IOException;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;

public class SimpleAppointmentListener {
	
	private static final Log log = LogFactory.getLog(SimpleAppointmentListener.class);
	
	private static final String WEBHOOK_URL = System.getenv("PP_WEBHOOK_URL");
	
	private static final String SECRETS_FILE = System.getenv("PP_SECRETS_FILE");
	
	private static final String GP_API_KEY = "patientpingeling.apiKey";
	
	private static final String GP_TENANT_ID = "patientpingeling.tenantId";
	
	private static final String ENV_SERVICE_USER = "PP_SERVICE_USER";
	
	private static final String ENV_SERVICE_PASSWORD = "PP_SERVICE_PASSWORD";
	
	private static final class Secrets {
		
		public String apiKey;
		
		public String tenantId;
		
		public String source;
		
		public boolean isValid() {
			return apiKey != null && !apiKey.trim().isEmpty() && tenantId != null && !tenantId.trim().isEmpty();
		}
	}
	
	private static String maskSecret(String value) {
		if (value == null) {
			return "<null>";
		}
		String trimmed = value.trim();
		if (trimmed.isEmpty()) {
			return "<empty>";
		}
		if (trimmed.length() <= 4) {
			return "<len=" + trimmed.length() + ">";
		}
		return trimmed.substring(0, 4) + "...<len=" + trimmed.length() + ">";
	}
	
	private static Secrets getSecrets() {
		// NOTE: do not cache secrets aggressively; admins may change them at runtime via UI (Global Properties).
		// 1) Preferred: explicit file path (Docker secret / mounted file)
		Secrets secretsFromFile = tryLoadSecretsFromFile(SECRETS_FILE);
		if (secretsFromFile != null && secretsFromFile.isValid()) {
			secretsFromFile.source = "pp_secrets_file";
			log.error("PP_SECRETS: Using secrets source=" + secretsFromFile.source);
			return secretsFromFile;
		}
		
		// 2) Default for .omod users: drop a file into the OpenMRS application data directory.
		String defaultSecretsPath = getDefaultSecretsFilePath();
		Secrets secretsFromDefaultLocation = tryLoadSecretsFromFile(defaultSecretsPath);
		if (secretsFromDefaultLocation != null && secretsFromDefaultLocation.isValid()) {
			secretsFromDefaultLocation.source = "openmrs_appdata_file";
			log.error("PP_SECRETS: Using secrets source=" + secretsFromDefaultLocation.source + " path="
			        + defaultSecretsPath);
			return secretsFromDefaultLocation;
		}
		
		// 3) OpenMRS UI: Global Properties (Administration -> Advanced Settings)
		Secrets secretsFromGlobalProperties = tryLoadSecretsFromGlobalProperties();
		if (secretsFromGlobalProperties != null && secretsFromGlobalProperties.isValid()) {
			secretsFromGlobalProperties.source = "openmrs_global_properties";
			log.error("PP_SECRETS: Using secrets source=" + secretsFromGlobalProperties.source + " keys=" + GP_API_KEY + ","
			        + GP_TENANT_ID);
			return secretsFromGlobalProperties;
		}
		
		// 4) Backwards compatible fallback: environment variables as before.
		Secrets envSecrets = new Secrets();
		envSecrets.apiKey = System.getenv("PP_API_KEY");
		envSecrets.tenantId = System.getenv("PP_TENANT_KEY");
		envSecrets.source = "env_vars";
		log.error("PP_SECRETS: Using secrets source=" + envSecrets.source + " (fallback). PP_SECRETS_FILE configured="
		        + (SECRETS_FILE != null));
		return envSecrets;
	}
	
	private static Secrets tryLoadSecretsFromGlobalProperties() {
		try {
			Secrets secrets = new Secrets();
			secrets.apiKey = Context.getAdministrationService().getGlobalProperty(GP_API_KEY);
			secrets.tenantId = Context.getAdministrationService().getGlobalProperty(GP_TENANT_ID);
			return secrets;
		}
		catch (Exception e) {
			// This can fail if there is no OpenMRS context/session available.
			return null;
		}
	}
	
	private static String getDefaultSecretsFilePath() {
		try {
			String appDataDirPath = OpenmrsUtil.getApplicationDataDirectory();
			File appDataDir = new File(appDataDirPath);
			File moduleDir = new File(appDataDir, "patientpingeling");
			return new File(moduleDir, "pp-secrets.json").getAbsolutePath();
		}
		catch (Exception e) {
			// If anything goes wrong, return a non-existent path so the caller can fall back.
			log.error("PP_SECRETS: Could not determine OpenMRS application data directory", e);
			return "";
		}
	}
	
	private static Secrets tryLoadSecretsFromFile(String secretsFilePath) {
		if (secretsFilePath == null || secretsFilePath.trim().isEmpty()) {
			return null;
		}
		try {
			Path path = Paths.get(secretsFilePath);
			if (!Files.exists(path)) {
				log.error("PP_SECRETS: Secrets file not found at: " + secretsFilePath);
				return null;
			}
			ObjectMapper mapper = new ObjectMapper();
			return mapper.readValue(new File(secretsFilePath), Secrets.class);
		}
		catch (Exception e) {
			log.error("PP_SECRETS: Failed to load/parse secrets JSON file", e);
			return null;
		}
	}
	
	private static final class ServiceCredentials {
		
		public String username;
		
		public String password;
		
		public String source;
		
		public boolean isValid() {
			return username != null && !username.trim().isEmpty() && password != null && !password.trim().isEmpty();
		}
	}
	
	private static ServiceCredentials getServiceCredentials() {
		// Docker Compose / env vars
		ServiceCredentials envCreds = new ServiceCredentials();
		envCreds.username = System.getenv(ENV_SERVICE_USER);
		envCreds.password = System.getenv(ENV_SERVICE_PASSWORD);
		envCreds.source = "env_vars";
		return envCreds;
	}
	
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
										    ServiceCredentials creds = getServiceCredentials();
										    if (!creds.isValid()) {
											    log.error("PP_AUTH: Missing service credentials. Configure env vars "
											            + ENV_SERVICE_USER + "/" + ENV_SERVICE_PASSWORD
											            + " for the OpenMRS container.");
											    return;
										    }
										    log.error("PP_AUTH: Authenticating with service user='" + creds.username
										            + "' source=" + creds.source);
										    Context.authenticate(creds.username, creds.password);
										    
										    EventEnricher enricher = new EventEnricher();
										    EnrichedEvent enriched = enricher.enrichAppointment(uuid, action);
										    
										    if (enriched != null) {
											    ObjectMapper mapper = new ObjectMapper();
											    String json = mapper.writeValueAsString(enriched);
											    Secrets secrets = getSecrets();
											    log.error("PP_WEBHOOK: URL=" + WEBHOOK_URL + " TENANT_NULL="
											            + (secrets.tenantId == null) + " API_KEY_NULL="
											            + (secrets.apiKey == null));
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
		Secrets secrets = getSecrets();
		log.error("PP_WEBHOOK: Secrets to be applied. source=" + secrets.source + " apiKey=" + maskSecret(secrets.apiKey)
		        + " tenantId=" + maskSecret(secrets.tenantId));
		
		for (int i = 1; i <= maxRetries; i++) {
			HttpURLConnection conn = null;
			OutputStream os = null;
			try {
				log.error("PP_WEBHOOK: Attempt " + i + " to send data...");
				
				URL url = new URL(WEBHOOK_URL);
				conn = (HttpURLConnection) url.openConnection();
				conn.setRequestMethod("POST");
				conn.setRequestProperty("Content-Type", "application/json; utf-8");
				log.error("PP_WEBHOOK: Setting headers X-Api-Key and X-Tenant-Id (masked). apiKey="
				        + maskSecret(secrets.apiKey) + " tenantId=" + maskSecret(secrets.tenantId));
				conn.setRequestProperty("X-Api-Key", secrets.apiKey);
				conn.setRequestProperty("X-Tenant-Id", secrets.tenantId);
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
