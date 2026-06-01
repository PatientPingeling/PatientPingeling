package org.openmrs.module.patientpingeling.enricher.event;

import org.openmrs.module.patientpingeling.enricher.model.EnrichedEvent;
import org.openmrs.module.patientpingeling.enricher.RetryQueueService;
import org.openmrs.module.patientpingeling.enricher.RetryWorker;
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
	
	private static final String SECRETS_SOURCE_LOG_PREFIX = "PP_SECRETS: Using secrets source=";
	
	private static final int MAX_RETRIES = 4;
	
	private static final int[] WAIT_TIMES = { 2000, 4000, 16000, 16000 };
	
	private SimpleAppointmentListener() {
	}
	
	private static final class Secrets {
		
		private String apiKey;
		
		private String tenantId;
		
		private String source;
		
		public String getApiKey() {
			return apiKey;
		}
		
		public void setApiKey(String apiKey) {
			this.apiKey = apiKey;
		}
		
		public String getTenantId() {
			return tenantId;
		}
		
		public void setTenantId(String tenantId) {
			this.tenantId = tenantId;
		}
		
		public String getSource() {
			return source;
		}
		
		public void setSource(String source) {
			this.source = source;
		}
		
		public boolean isValid() {
			return apiKey != null && !apiKey.trim().isEmpty() && tenantId != null && !tenantId.trim().isEmpty();
		}
	}
	
	private static Secrets getSecrets() {
		// NOTE: do not cache secrets aggressively; admins may change them at runtime via UI (Global Properties).
		// 1) Preferred: explicit file path (Docker secret / mounted file)
		Secrets secretsFromFile = tryLoadSecretsFromFile(SECRETS_FILE);
		if (secretsFromFile != null && secretsFromFile.isValid()) {
			secretsFromFile.source = "pp_secrets_file";
			log.error(SECRETS_SOURCE_LOG_PREFIX + secretsFromFile.source);
			return secretsFromFile;
		}
		
		// 2) Default for .omod users: drop a file into the OpenMRS application data directory.
		String defaultSecretsPath = getDefaultSecretsFilePath();
		Secrets secretsFromDefaultLocation = tryLoadSecretsFromFile(defaultSecretsPath);
		if (secretsFromDefaultLocation != null && secretsFromDefaultLocation.isValid()) {
			secretsFromDefaultLocation.source = "openmrs_appdata_file";
			log.error(SECRETS_SOURCE_LOG_PREFIX + secretsFromDefaultLocation.source + " path="
			        + defaultSecretsPath);
			return secretsFromDefaultLocation;
		}
		
		// 3) OpenMRS UI: Global Properties (Administration -> Advanced Settings)
		Secrets secretsFromGlobalProperties = tryLoadSecretsFromGlobalProperties();
		if (secretsFromGlobalProperties != null && secretsFromGlobalProperties.isValid()) {
			secretsFromGlobalProperties.source = "openmrs_global_properties";
			log.error(SECRETS_SOURCE_LOG_PREFIX + secretsFromGlobalProperties.source + " keys=" + GP_API_KEY + ","
			        + GP_TENANT_ID);
			return secretsFromGlobalProperties;
		}
		
		// 4) Backwards compatible fallback: environment variables as before.
		Secrets envSecrets = new Secrets();
		envSecrets.apiKey = System.getenv("PP_API_KEY");
		envSecrets.tenantId = System.getenv("PP_TENANT_KEY");
		envSecrets.source = "env_vars";
		log.error(SECRETS_SOURCE_LOG_PREFIX + envSecrets.source + " (fallback). PP_SECRETS_FILE configured="
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
		
		private String username;
		
		private String password;
		
		private String source;
		
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
		    (InvocationHandler) (proxy, method, args) -> {
			    if (isOnMessage(method, args)) {
				    handleMessage(args[0], mapMessageClass);
			    }
			    return null;
		    });
	}
	
	private static boolean isOnMessage(Method method, Object[] args) {
		return "onMessage".equals(method.getName()) && args != null && args.length == 1;
	}
	
	private static void handleMessage(Object message, Class<?> mapMessageClass) {
		try {
			if (!mapMessageClass.isInstance(message)) {
				return;
			}
			
			Method getString = mapMessageClass.getMethod("getString", String.class);
			String action = (String) getString.invoke(message, "action");
			String uuid = (String) getString.invoke(message, "uuid");
			log.error("PP_EVENT_LOG: Received " + action + " for UUID: " + uuid);
			startEventProcessingThread(uuid, action);
		}
		catch (Exception e) {
			log.error("PP_ENRICHER: Proxy error", e);
		}
	}
	
	private static void startEventProcessingThread(final String uuid, final String action) {
		Thread t = new Thread(() -> processEvent(uuid, action));
		t.start();
	}
	
	private static void processEvent(String uuid, String action) {
		try {
			Context.openSession();
			if (!authenticateServiceUser()) {
				return;
			}
			
			EventEnricher enricher = new EventEnricher();
			EnrichedEvent enriched = enricher.enrichAppointment(uuid, action);
			if (enriched != null) {
				sendEnrichedEvent(enriched, uuid, action);
			}
		}
		catch (Exception e) {
			log.error("PP_FATAL: Thread crashed", e);
		}
		finally {
			Context.closeSession();
		}
	}
	
	private static boolean authenticateServiceUser() {
		ServiceCredentials creds = getServiceCredentials();
		if (!creds.isValid()) {
			log.error("PP_AUTH: Missing service credentials. Configure env vars " + ENV_SERVICE_USER + "/"
			        + ENV_SERVICE_PASSWORD + " for the OpenMRS container.");
			return false;
		}
		
		log.error("PP_AUTH: Authenticating with service user='" + creds.username + "' source=" + creds.source);
		Context.authenticate(creds.username, creds.password);
		return true;
	}
	
	private static void sendEnrichedEvent(EnrichedEvent enriched, String uuid, String action) throws IOException {
		ObjectMapper mapper = new ObjectMapper();
		String json = mapper.writeValueAsString(enriched);
		Secrets secrets = getSecrets();
		log.error("PP_WEBHOOK: URL=" + WEBHOOK_URL + " TENANT_NULL=" + (secrets.tenantId == null) + " API_KEY_NULL="
		        + (secrets.apiKey == null));
		webHookCaller(json, uuid, action);
	}
	
	public static boolean webHookCaller(String jsonPayload, String uuid, String action) {
		return webHookCaller(jsonPayload, uuid, action, true);
	}
	
	public static boolean webHookCaller(String jsonPayload, String uuid, String action, boolean persistOnFailure) {
		log.error("PP_WEBHOOK: Starting caller. URL is: " + WEBHOOK_URL);
		Secrets secrets = getSecrets();
		
		for (int i = 1; i <= MAX_RETRIES; i++) {
			try {
				log.error("PP_WEBHOOK: Attempt " + i + " to send data...");
				WebhookResult result = sendWebhookAttempt(jsonPayload, uuid, secrets);
				if (result.isHandled()) {
					return true;
				}
			}
			catch (IOException e) {
				log.error("PP_WEBHOOK: Attempt " + i + " failed: " + e.getMessage());
				if (!prepareNextRetry(i, uuid, action, jsonPayload, persistOnFailure)) {
					return false;
				}
			}
		}
		return false;
	}
	
	private static WebhookResult sendWebhookAttempt(String jsonPayload, String uuid, Secrets secrets) throws IOException {
		HttpURLConnection conn = null;
		try {
			URL url = new URL(WEBHOOK_URL);
			conn = (HttpURLConnection) url.openConnection();
			conn.setRequestMethod("POST");
			conn.setRequestProperty("Content-Type", "application/json; utf-8");
			conn.setRequestProperty("X-Api-Key", secrets.apiKey);
			conn.setRequestProperty("X-Tenant-Id", secrets.tenantId);
			conn.setDoOutput(true);
			conn.setConnectTimeout(5000);
			
			writePayload(conn, jsonPayload);
			return interpretResponse(conn.getResponseCode(), uuid);
		}
		finally {
			if (conn != null) {
				conn.disconnect();
			}
		}
	}
	
	private static void writePayload(HttpURLConnection conn, String jsonPayload) throws IOException {
		try (OutputStream os = conn.getOutputStream()) {
			byte[] input = jsonPayload.getBytes(StandardCharsets.UTF_8);
			os.write(input, 0, input.length);
			os.flush();
		}
	}
	
	private static WebhookResult interpretResponse(int code, String uuid) throws IOException {
		if (code >= 200 && code < 300) {
			log.error("PP_WEBHOOK: Success! Status code: " + code);
			return WebhookResult.HANDLED;
		}
		if (code == 400 || code == 404 || code == 422) {
			log.error("PP_WEBHOOK: Client error " + code + " for uuid=" + uuid + ", not retrying.");
			return WebhookResult.HANDLED;
		}
		throw new IOException("HTTP error code: " + code);
	}
	
	private static boolean prepareNextRetry(int attempt, String uuid, String action, String jsonPayload,
	        boolean persistOnFailure) {
		if (attempt < MAX_RETRIES) {
			return waitBeforeRetry(attempt);
		}
		
		log.error("PP_WEBHOOK: Max retries reached for uuid=" + uuid + " action=" + action + " persistOnFailure="
		        + persistOnFailure);
		persistRetryIfNeeded(uuid, action, jsonPayload, persistOnFailure);
		return false;
	}
	
	private static boolean waitBeforeRetry(int attempt) {
		try {
			int wait = WAIT_TIMES[attempt - 1];
			log.error("PP_WEBHOOK: Waiting " + (wait / 1000) + " seconds before retry...");
			Thread.sleep(wait);
			return true;
		}
		catch (InterruptedException ie) {
			Thread.currentThread().interrupt();
			return false;
		}
	}
	
	private static void persistRetryIfNeeded(String uuid, String action, String jsonPayload, boolean persistOnFailure) {
		if (!persistOnFailure) {
			return;
		}
		
		Long id = RetryQueueService.insert(uuid, action, jsonPayload);
		if (id != null) {
			RetryWorker.enqueue(id);
		}
	}
	
	private enum WebhookResult {
		HANDLED;
		
		boolean isHandled() {
			return this == HANDLED;
		}
	}
}
