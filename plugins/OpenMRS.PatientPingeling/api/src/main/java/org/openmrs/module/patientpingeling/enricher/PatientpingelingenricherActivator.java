package org.openmrs.module.patientpingeling.enricher;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.module.BaseModuleActivator;
import org.openmrs.module.ModuleFactory;
import org.openmrs.module.DaemonToken; // Import toegevoegd
import org.openmrs.module.patientpingeling.enricher.event.EventSubscriber;

public class PatientpingelingenricherActivator extends BaseModuleActivator {
	
	private static DaemonToken daemonToken; // Statisch opslaan zodat we erbij kunnen
	
	public static DaemonToken getDaemonToken() {
		return daemonToken;
	}
	
	public void setDaemonToken(DaemonToken token) {
		daemonToken = token;
	}
	
	private final Log log = LogFactory.getLog(this.getClass());
	
	@Override
	public void started() {
		log.error("PP_ENRICHER: started() called");
		try {
			EventSubscriber.subscribe();
			log.error("PP_ENRICHER: subscribe completed");
		}
		catch (Exception e) {
			log.error("PP_ENRICHER: subscribe failed", e);
		}
	}
	
	@Override
	public void stopped() {
		log.info("PP_ENRICHER: stopped() called");
		if (ModuleFactory.isModuleStarted("event")) {
			try {
				EventSubscriber.unsubscribe();
			}
			catch (Exception e) {
				log.error("PP_ENRICHER: Unsubscribe failed", e);
			}
		}
	}
}
