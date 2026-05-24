package org.openmrs.module.patientpingeling.enricher;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.api.context.Context;
import org.openmrs.module.patientpingeling.enricher.event.SimpleAppointmentListener;

import java.util.concurrent.LinkedBlockingQueue;

public class RetryWorker {
	
	private static final Log log = LogFactory.getLog(RetryWorker.class);
	
	private static final LinkedBlockingQueue<Long> queue = new LinkedBlockingQueue<>();
	
	private static Thread workerThread;
	
	public static void start() {
		workerThread = new Thread(new Runnable() {
			
			@Override
			public void run() {
				log.error("PP_WORKER: Retry worker started.");
				while (!Thread.currentThread().isInterrupted()) {
					try {
						Long id = queue.take();
						log.error("PP_WORKER: Picked up retry row id=" + id + ", attempting immediately...");
						
						Context.openSession();
						try {
							ServiceCredentials creds = getServiceCredentials();
							if (creds.isValid()) {
								Context.authenticate(creds.username, creds.password);
							}
							String[] row = RetryQueueService.loadRow(id);
							if (row == null) {
								log.error("PP_WORKER: Row id=" + id + " no longer exists, skipping.");
								continue;
							}
							String uuid = row[0];
							String action = row[1];
							String payload = row[2];
							log.error("PP_WORKER: Retrying webhook for uuid=" + uuid + " action=" + action);
							boolean success = SimpleAppointmentListener.webHookCaller(payload, uuid, action, false);
							if (success) {
								RetryQueueService.delete(id);
								log.error("PP_WORKER: Success, removed row id=" + id);
							} else {
								log.error("PP_WORKER: Still failing, waiting 30 seconds before re-queuing row id=" + id);
								Thread.sleep(30000);
								queue.offer(id);
							}
						}
						finally {
							Context.closeSession();
						}
					}
					catch (InterruptedException ie) {
						Thread.currentThread().interrupt();
					}
					catch (Exception e) {
						log.error("PP_WORKER: Unexpected error in retry worker", e);
					}
				}
				log.error("PP_WORKER: Retry worker stopped.");
			}
		});
		workerThread.setDaemon(true);
		workerThread.start();
	}
	
	public static void stop() {
		if (workerThread != null) {
			workerThread.interrupt();
		}
	}
	
	public static void enqueue(Long id) {
		log.error("PP_WORKER: Enqueuing retry for DB row id=" + id);
		queue.offer(id);
	}
	
	public static void loadFromDatabase() {
		log.error("PP_WORKER: Loading unprocessed rows from DB...");
		for (long[] row : RetryQueueService.loadAll()) {
			queue.offer(row[0]);
			log.error("PP_WORKER: Re-enqueued surviving row id=" + row[0]);
		}
	}
	
	private static ServiceCredentials getServiceCredentials() {
		ServiceCredentials creds = new ServiceCredentials();
		creds.username = System.getenv("PP_SERVICE_USER");
		creds.password = System.getenv("PP_SERVICE_PASSWORD");
		return creds;
	}
	
	static class ServiceCredentials {
		String username;
		String password;
		boolean isValid() {
			return username != null && !username.trim().isEmpty() && password != null && !password.trim().isEmpty();
		}
	}
}