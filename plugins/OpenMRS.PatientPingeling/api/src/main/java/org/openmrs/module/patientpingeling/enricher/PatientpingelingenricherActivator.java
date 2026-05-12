package org.openmrs.module.patientpingeling.enricher;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.module.BaseModuleActivator;
import org.openmrs.module.ModuleFactory;
import org.openmrs.module.patientpingeling.enricher.event.EventSubscriber;

public class PatientpingelingenricherActivator extends BaseModuleActivator {
	
	static {
		System.out.println("PP_ENRICHER_STATIC: Class loaded!");
	}
	
	private final Log log = LogFactory.getLog(this.getClass());
	
	@Override
	public void started() {
		System.out.println("PP_ENRICHER_STATIC: started() called!");
		log.error("PP_ENRICHER: started() called");
		log.error("PP_ENRICHER: event started = " + ModuleFactory.isModuleStarted("event"));
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
		
		boolean eventStarted = ModuleFactory.isModuleStarted("event");
		log.info("PP_ENRICHER: isModuleStarted('event') = " + eventStarted);
		
		if (eventStarted) {
			try {
				EventSubscriber.unsubscribe();
				log.info("PP_ENRICHER: Unsubscribed from Event module.");
			}
			catch (Exception e) {
				log.error("PP_ENRICHER: Unsubscribe failed", e);
			}
		}
	}
}
