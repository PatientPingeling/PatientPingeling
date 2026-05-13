package org.openmrs.module.patientpingeling.enricher.Model;

import java.time.LocalDateTime;

public class EnrichedEvent {
	
	// Enum voor duidelijke acties
	public enum AppointmentAction {
		CREATED, UPDATED, CANCELLED, UNKNOWN
	}
	
	private String uuid;
	
	private AppointmentAction action;
	
	private String status; // De ruwe status uit DB (Scheduled/Cancelled)
	
	private String appointmentService; // Bijv. 'General Medicine'
	
	private String locatie;
	
	private LocalDateTime datumEnTijd;
	
	private String comments;
	
	private String email;
	
	private String tel;
	
	private String naam;
	
	// Constructors
	public EnrichedEvent() {
	}
	
	public EnrichedEvent(String uuid, AppointmentAction action, String status, String appointmentService, String locatie,
	    LocalDateTime datumEnTijd, String comments, String email, String tel, String naam) {
		this.uuid = uuid;
		this.action = action;
		this.status = status;
		this.appointmentService = appointmentService;
		this.locatie = locatie;
		this.datumEnTijd = datumEnTijd;
		this.comments = comments;
		this.email = email;
		this.tel = tel;
		this.naam = naam;
	}
	
	// Getters and Setters
	public String getUuid() {
		return uuid;
	}
	
	public void setUuid(String uuid) {
		this.uuid = uuid;
	}
	
	public AppointmentAction getAction() {
		return action;
	}
	
	public void setAction(AppointmentAction action) {
		this.action = action;
	}
	
	public String getStatus() {
		return status;
	}
	
	public void setStatus(String status) {
		this.status = status;
	}
	
	public String getAppointmentService() {
		return appointmentService;
	}
	
	public void setAppointmentService(String appointmentService) {
		this.appointmentService = appointmentService;
	}
	
	public String getLocatie() {
		return locatie;
	}
	
	public void setLocatie(String locatie) {
		this.locatie = locatie;
	}
	
	public LocalDateTime getDatumEnTijd() {
		return datumEnTijd;
	}
	
	public void setDatumEnTijd(LocalDateTime datumEnTijd) {
		this.datumEnTijd = datumEnTijd;
	}
	
	public String getComments() {
		return comments;
	}
	
	public void setComments(String comments) {
		this.comments = comments;
	}
	
	public String getEmail() {
		return email;
	}
	
	public void setEmail(String email) {
		this.email = email;
	}
	
	public String getTel() {
		return tel;
	}
	
	public void setTel(String tel) {
		this.tel = tel;
	}
	
	public String getNaam() {
		return naam;
	}
	
	public void setNaam(String naam) {
		this.naam = naam;
	}
	
	@Override
	public String toString() {
		return "EnrichedEvent{" + "uuid='" + uuid + '\'' + ", action=" + action + ", status='" + status + '\''
		        + ", service='" + appointmentService + '\'' + ", naam='" + naam + '\'' + ", locatie='" + locatie + '\''
		        + ", datumEnTijd=" + datumEnTijd + ", email='" + email + '\'' + ", tel='" + tel + '\'' + ", comments='"
		        + comments + '\'' + '}';
	}
}
