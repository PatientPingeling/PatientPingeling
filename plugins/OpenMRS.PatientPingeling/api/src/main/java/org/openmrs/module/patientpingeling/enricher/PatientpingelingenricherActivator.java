package org.openmrs.module.patientpingeling.enricher;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.GlobalProperty;
import org.openmrs.api.AdministrationService;
import org.openmrs.api.context.Context;
import org.openmrs.module.BaseModuleActivator;
import org.openmrs.module.patientpingeling.enricher.event.EventSubscriber;

public class PatientpingelingenricherActivator extends BaseModuleActivator {
	
	private final Log log = LogFactory.getLog(this.getClass());
	
	private static final String GP_API_KEY = "patientpingeling.apiKey";
	
	private static final String GP_TENANT_ID = "patientpingeling.tenantId";
	
	@Override
	public void started() {
		log.error("PP_ENRICHER: Module gestart. Subscribing...");
		try {
			ensureGlobalPropertiesExist();
			EventSubscriber.subscribe();
			log.error("PP_ENRICHER: Subscribe voltooid.");
		}
		catch (Exception e) {
			log.error("PP_ENRICHER: Subscribe mislukt", e);
		}
	}
	
	private void ensureGlobalPropertiesExist() {
		try {
			AdministrationService admin = Context.getAdministrationService();
			ensureGlobalProperty(admin, GP_API_KEY, "API key used for PatientPingeling webhook calls. Treat as secret.");
			ensureGlobalProperty(admin, GP_TENANT_ID, "Tenant id used for PatientPingeling webhook calls. Treat as secret.");
		}
		catch (Exception e) {
			log.error("PP_ENRICHER: Failed to ensure Global Properties exist", e);
		}
	}
	
	private void ensureGlobalProperty(AdministrationService admin, String propertyName, String description) {
		GlobalProperty existing = admin.getGlobalPropertyObject(propertyName);
		if (existing != null) {
			return;
		}
		
		GlobalProperty gp = new GlobalProperty();
		gp.setProperty(propertyName);
		gp.setPropertyValue("");
		gp.setDescription(description);
		admin.saveGlobalProperty(gp);
		log.error("PP_ENRICHER: Created missing Global Property: " + propertyName);
	}
	
	@Override
	public void stopped() {
		log.error("PP_ENRICHER: Module gestopt.");
		try {
			EventSubscriber.unsubscribe();
		}
		catch (Exception e) {
			log.error("PP_ENRICHER: Unsubscribe mislukt", e);
		}
	}
}
