package org.openmrs.module.patientpingeling.enricher;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.openmrs.util.DatabaseUpdater;

import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.Statement;
import java.util.ArrayList;
import java.util.List;

public class RetryQueueService {
	
	private static final Log log = LogFactory.getLog(RetryQueueService.class);
	
	private RetryQueueService() {
	}
	
	private static Connection getConnection() throws Exception {
		return DatabaseUpdater.getConnection();
	}
	
	public static void ensureTableExists() {
		try (Connection conn = getConnection()) {
			try (Statement stmt = conn.createStatement()) {
				stmt.execute(
				    "CREATE TABLE IF NOT EXISTS patientpingeling_retry_queue ("
				            + "id INT AUTO_INCREMENT PRIMARY KEY,"
				            + "uuid VARCHAR(38) NOT NULL,"
				            + "action VARCHAR(50) NOT NULL,"
				            + "payload MEDIUMTEXT NOT NULL,"
				            + "created_at DATETIME DEFAULT CURRENT_TIMESTAMP"
				            + ")");
				log.error("PP_QUEUE: Table patientpingeling_retry_queue ensured.");
			}
		}
		catch (Exception e) {
			log.error("PP_QUEUE: Failed to create retry queue table", e);
		}
	}
	
	public static Long insert(String uuid, String action, String payload) {
		try (Connection conn = getConnection()) {
			try (PreparedStatement ps = conn.prepareStatement(
			    "INSERT INTO patientpingeling_retry_queue (uuid, action, payload) VALUES (?, ?, ?)",
			    Statement.RETURN_GENERATED_KEYS)) {
				ps.setString(1, uuid);
				ps.setString(2, action);
				ps.setString(3, payload);
				ps.executeUpdate();
				try (ResultSet rs = ps.getGeneratedKeys()) {
					if (rs.next()) {
						long id = rs.getLong(1);
						log.error("PP_QUEUE: Inserted retry row id=" + id + " uuid=" + uuid);
						return id;
					}
				}
			}
		}
		catch (Exception e) {
			log.error("PP_QUEUE: Failed to insert retry row for uuid=" + uuid, e);
		}
		return null;
	}
	
	public static void delete(Long id) {
		try (Connection conn = getConnection()) {
			try (PreparedStatement ps = conn.prepareStatement(
			    "DELETE FROM patientpingeling_retry_queue WHERE id = ?")) {
				ps.setLong(1, id);
				ps.executeUpdate();
				log.error("PP_QUEUE: Deleted retry row id=" + id);
			}
		}
		catch (Exception e) {
			log.error("PP_QUEUE: Failed to delete retry row id=" + id, e);
		}
	}
	
	public static List<long[]> loadAll() {
		List<long[]> rows = new ArrayList<>();
		try (Connection conn = getConnection()) {
			try (Statement stmt = conn.createStatement()) {
				try (ResultSet rs = stmt.executeQuery(
				    "SELECT id FROM patientpingeling_retry_queue ORDER BY created_at ASC")) {
					while (rs.next()) {
						rows.add(new long[] { rs.getLong("id") });
					}
				}
			}
		}
		catch (Exception e) {
			log.error("PP_QUEUE: Failed to load retry queue", e);
		}
		return rows;
	}
	
	public static String[] loadRow(long id) {
		try (Connection conn = getConnection()) {
			try (PreparedStatement ps = conn.prepareStatement(
			    "SELECT uuid, action, payload FROM patientpingeling_retry_queue WHERE id = ?")) {
				ps.setLong(1, id);
				try (ResultSet rs = ps.executeQuery()) {
					if (rs.next()) {
						return new String[] { rs.getString("uuid"), rs.getString("action"), rs.getString("payload") };
					}
				}
			}
		}
		catch (Exception e) {
			log.error("PP_QUEUE: Failed to load row id=" + id, e);
		}
		return new String[0];
	}
}
