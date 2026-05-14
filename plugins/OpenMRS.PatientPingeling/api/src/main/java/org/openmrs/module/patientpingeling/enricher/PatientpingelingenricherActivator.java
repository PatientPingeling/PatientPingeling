package org.openmrs.module.patientpingeling.enricher;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.module.BaseModuleActivator;
import org.openmrs.module.patientpingeling.enricher.event.EventSubscriber;

public class PatientpingelingenricherActivator extends BaseModuleActivator {
	
	private final Log log = LogFactory.getLog(this.getClass());
	
	@Override
	public void started() {
		log.error("PP_ENRICHER: Module gestart. Subscribing...");
		try {
			EventSubscriber.subscribe();
			log.error("PP_ENRICHER: Subscribe voltooid.");
		}
		catch (Exception e) {
			log.error("PP_ENRICHER: Subscribe mislukt", e);
		}
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
