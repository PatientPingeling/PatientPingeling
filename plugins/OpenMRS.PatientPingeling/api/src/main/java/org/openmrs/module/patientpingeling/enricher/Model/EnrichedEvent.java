package org.openmrs.module.patientpingeling.enricher.Model;

public class EnrichedEvent {
	
	public enum AppointmentAction {
		CREATED, UPDATED, CANCELLED, UNKNOWN
	}
	
	private AppointmentAction action;
	
	private PatientDto patient;
	
	private AppointmentDto appointment;
	
	// -- Nested: Patient --
	public static class PatientDto {
		
		private String externalId;
		
		private String givenName;
		
		private String email;
		
		private String phoneNumber;
		
		public PatientDto() {
		}
		
		public PatientDto(String externalId, String givenName, String email, String phoneNumber) {
			this.externalId = externalId;
			this.givenName = givenName;
			this.email = email;
			this.phoneNumber = phoneNumber;
		}
		
		public String getExternalId() {
			return externalId;
		}
		
		public void setExternalId(String externalId) {
			this.externalId = externalId;
		}
		
		public String getGivenName() {
			return givenName;
		}
		
		public void setGivenName(String givenName) {
			this.givenName = givenName;
		}
		
		public String getEmail() {
			return email;
		}
		
		public void setEmail(String email) {
			this.email = email;
		}
		
		public String getPhoneNumber() {
			return phoneNumber;
		}
		
		public void setPhoneNumber(String phoneNumber) {
			this.phoneNumber = phoneNumber;
		}
		
		@Override
		public String toString() {
			return "PatientDto{externalId='" + externalId + "', givenName='" + givenName + "', email='" + email
			        + "', phoneNumber='" + phoneNumber + "'}";
		}
	}
	
	// -- Nested: Appointment --
	public static class AppointmentDto {
		
		private String externalId;
		
		private String scheduledAt; // ISO-8601 in UTC e.g. "2026-06-01T08:00:00+00:00" — server converts to tenant timezone for patient messages.
		
		private String service;
		
		private String location;
		
		private String instructions;
		
		public AppointmentDto() {
		}
		
		public AppointmentDto(String externalId, String scheduledAt, String service, String location, String instructions) {
			this.externalId = externalId;
			this.scheduledAt = scheduledAt;
			this.service = service;
			this.location = location;
			this.instructions = instructions;
		}
		
		public String getExternalId() {
			return externalId;
		}
		
		public void setExternalId(String externalId) {
			this.externalId = externalId;
		}
		
		public String getScheduledAt() {
			return scheduledAt;
		}
		
		public void setScheduledAt(String scheduledAt) {
			this.scheduledAt = scheduledAt;
		}
		
		public String getService() {
			return service;
		}
		
		public void setService(String service) {
			this.service = service;
		}
		
		public String getLocation() {
			return location;
		}
		
		public void setLocation(String location) {
			this.location = location;
		}
		
		public String getInstructions() {
			return instructions;
		}
		
		public void setInstructions(String instructions) {
			this.instructions = instructions;
		}
		
		@Override
		public String toString() {
			return "AppointmentDto{externalId='" + externalId + "', scheduledAt='" + scheduledAt + "', service='" + service
			        + "', location='" + location + "', instructions='" + instructions + "'}";
		}
	}
	
	// -- Constructors --
	public EnrichedEvent() {
	}
	
	public EnrichedEvent(AppointmentAction action, PatientDto patient, AppointmentDto appointment) {
		this.action = action;
		this.patient = patient;
		this.appointment = appointment;
	}
	
	// -- Getters & Setters --
	public AppointmentAction getAction() {
		return action;
	}
	
	public void setAction(AppointmentAction action) {
		this.action = action;
	}
	
	public PatientDto getPatient() {
		return patient;
	}
	
	public void setPatient(PatientDto patient) {
		this.patient = patient;
	}
	
	public AppointmentDto getAppointment() {
		return appointment;
	}
	
	public void setAppointment(AppointmentDto appointment) {
		this.appointment = appointment;
	}
	
	@Override
	public String toString() {
		return "EnrichedEvent{action=" + action + ", patient=" + patient + ", appointment=" + appointment + "}";
	}
}
