/**
 * This Source Code Form is subject to the terms of the Mozilla Public License,
 * v. 2.0. If a copy of the MPL was not distributed with this file, You can
 * obtain one at http://mozilla.org/MPL/2.0/. OpenMRS is also distributed under
 * the terms of the Healthcare Disclaimer located at http://openmrs.org/license.
 *
 * Copyright (C) OpenMRS Inc. OpenMRS is a registered trademark and the OpenMRS
 * graphic logo is a trademark of OpenMRS Inc.
 */
package org.openmrs.module.patientpingeling.enricher.event;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;

/**
 * Logs events published by the OpenMRS Event Module. The Event Module uses JMS (ActiveMQ) and
 * delivers events as a JMS MapMessage with keys like "action", "classname" and "uuid". This class
 * intentionally avoids compile-time references to the Event Module and JMS APIs so that this module
 * can still start even if the Event Module is not installed.
 */
public class EventModuleEventLoggingListener {
	
	private static final Log log = LogFactory.getLog(EventModuleEventLoggingListener.class);
	
	private static final String DOCKER_LOG_PREFIX = "PP_ENRICHER_EVENT";
	
	public void onMessage(Object message) {
		if (message == null) {
			logAndPrint(DOCKER_LOG_PREFIX + " message=<null>");
			return;
		}
		
		try {
			String destination = getDestinationString(message);
			String action = getMapString(message, "action");
			String className = getMapString(message, "classname");
			String uuid = getMapString(message, "uuid");
			
			if (!isBlank(action) || !isBlank(className) || !isBlank(uuid)) {
				logAndPrint(DOCKER_LOG_PREFIX + " topic=" + destination + " action=" + action + " class=" + className
				        + " uuid=" + uuid);
			} else {
				logAndPrint(DOCKER_LOG_PREFIX + " topic=" + destination + " messageType=" + message.getClass().getName()
				        + " payload=" + String.valueOf(message));
			}
		}
		catch (Exception e) {
			log.error(DOCKER_LOG_PREFIX + " failed_to_read_message", e);
		}
	}
	
	private String getDestinationString(Object message) {
		try {
			Object destination = message.getClass().getMethod("getJMSDestination").invoke(message);
			if (destination == null) {
				return "<unknown>";
			}
			
			try {
				Object topicName = destination.getClass().getMethod("getTopicName").invoke(destination);
				if (topicName != null) {
					return topicName.toString();
				}
			}
			catch (NoSuchMethodException ignored) {
				// not a Topic
			}
			
			return destination.toString();
		}
		catch (Exception ignored) {
			return "<unknown>";
		}
	}
	
	private String getMapString(Object message, String key) {
		if (message == null || key == null) {
			return "";
		}
		
		try {
			Object exists = message.getClass().getMethod("itemExists", String.class).invoke(message, key);
			if (exists instanceof Boolean && !((Boolean) exists)) {
				return "";
			}
			
			Object value = message.getClass().getMethod("getString", String.class).invoke(message, key);
			return value == null ? "" : value.toString();
		}
		catch (NoSuchMethodException ignored) {
			// not a MapMessage
			return "";
		}
		catch (Exception ignored) {
			return "";
		}
	}
	
	private boolean isBlank(String value) {
		return value == null || value.trim().isEmpty();
	}
	
	private void logAndPrint(String line) {
		log.info(line);
		System.out.println(line);
	}
}
