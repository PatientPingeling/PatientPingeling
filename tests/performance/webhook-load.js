// k6 load test for the appointment webhook endpoint.
// Fires CREATED webhooks at the API with unique externalIds per iteration so
// every request actually hits the full ingest → DB → scheduled_notifications
// path instead of returning the duplicate-shortcut.
//
// Run via scripts/run-perf-test.sh (wraps k6 in Docker against your stack).

import http from "k6/http";
import { check } from "k6";

const BASE_URL = __ENV.BASE_URL || "http://host.docker.internal:8000";
// LegacyLink tenant: no rate limit, safe for full-flow demos.
// SwiftSend/SecurePost/AsyncFlow tenants are capped at 10 req/min — avoid for dispatch testing.
const TENANT_ID = __ENV.TENANT_ID || "5fa85f64-5717-4562-b3fc-2c963f66afa8";
const API_KEY = __ENV.API_KEY || "test-secret";

export const options = {
  // Staircase ramp: each step holds long enough to show up clearly in Grafana.
  stages: [
    { duration: "1m", target: 20 },  // ramp 0 → 20 VUs  (warm up)
    { duration: "2m", target: 20 },  // hold  20 VUs
    { duration: "1m", target: 50 },  // ramp 20 → 50 VUs
    { duration: "2m", target: 50 },  // hold  50 VUs
    { duration: "1m", target: 100 }, // ramp 50 → 100 VUs
    { duration: "2m", target: 100 }, // hold 100 VUs (peak)
    { duration: "1m", target: 0 },   // cool down
  ],
  // Thresholds — fail the run if these are breached.
  thresholds: {
    http_req_failed: ["rate<0.01"],    // <1% error rate
    http_req_duration: ["p(95)<1000"], // relaxed to 1s — 100 VUs is heavy load
  },
};

export default function () {
  // Unique IDs per iteration so we don't trigger the duplicate-shortcut.
  const runId = `${__VU}-${__ITER}-${Date.now()}`;
  const patientId = `PP-PERF-${runId}`;
  const apptId = `APT-PERF-${runId}`;

  // Appointment 3 minutes in the future — short enough that the scheduler picks it up
  // and dispatches it during the test run, making business metrics visible in Grafana.
  const scheduledAt = new Date(Date.now() + 3 * 60 * 1000).toISOString();

  const payload = JSON.stringify({
    action: "CREATED",
    patient: {
      externalId: patientId,
      givenName: "PerfTest",
      email: `${patientId}@example.com`,
      phoneNumber: "+31612345678",
    },
    appointment: {
      externalId: apptId,
      scheduledAt: scheduledAt,
      service: "PerfTest Service",
      location: "PerfTest Location",
      instructions: null,
    },
  });

  const res = http.post(`${BASE_URL}/webhooks/appointments`, payload, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": TENANT_ID,
      "X-Api-Key": API_KEY,
    },
    tags: { name: "webhook_create" },
  });

  check(res, {
    "status is 201": (r) => r.status === 201,
  });
}
