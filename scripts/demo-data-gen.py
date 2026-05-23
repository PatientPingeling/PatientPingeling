#!/usr/bin/env python3
"""
PatientPingeling — Demo Data Generator
Sends webhook requests to the API every 15 seconds, rotating through all 4
providers and mixing CREATED / UPDATED / CANCELLED actions so all Grafana
panels fill up during a live demo.

Usage:
    python3 scripts/demo-data-gen.py
    python3 scripts/demo-data-gen.py --interval 10 --url http://localhost:8000
    python3 scripts/demo-data-gen.py --interval dynamic   # random 1-10s per request
"""

import argparse
import json
import random
import time
import uuid
from datetime import datetime, timezone, timedelta
import urllib.request
import urllib.error

# ── Config ──────────────────────────────────────────────────────────────────

API_KEY = "test-secret"

TENANTS = [
    {"id": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "name": "SwiftSend"},
    {"id": "4fa85f64-5717-4562-b3fc-2c963f66afa7", "name": "SecurePost"},
    {"id": "5fa85f64-5717-4562-b3fc-2c963f66afa8", "name": "LegacyLink"},
    {"id": "6fa85f64-5717-4562-b3fc-2c963f66afa9", "name": "AsyncFlow"},
]

SERVICES = ["Cardiologie", "Neurologie", "Orthopedie", "Dermatologie", "Radiologie"]
LOCATIONS = ["Kamer A1", "Kamer B3", "Poli 2", "Afdeling 4", "Spreekkamer 7"]
INSTRUCTIONS = [
    "Neem uw vorige testresultaten mee",
    "Nuchter verschijnen",
    "Breng uw medicijnlijst mee",
    "Comfortabele kleding dragen",
    None,
]
FIRST_NAMES = ["Jan", "Sophie", "Mohammed", "Lisa", "Pieter", "Fatima", "Daan", "Emma"]
LAST_NAMES  = ["de Vries", "Jansen", "Bakker", "van den Berg", "Visser"]

# ── State ────────────────────────────────────────────────────────────────────

created_appointments: list[dict] = []   # track for UPDATE / CANCEL


# ── Helpers ──────────────────────────────────────────────────────────────────

def short_id() -> str:
    return uuid.uuid4().hex[:8].upper()


def scheduled_at_soon() -> str:
    """30 min from now — both reminders (24h and 1h) clamp to now and dispatch immediately."""
    dt = datetime.now(timezone.utc) + timedelta(minutes=30)
    return dt.strftime("%Y-%m-%dT%H:%M:%S+00:00")


def scheduled_at_future() -> str:
    """2–7 days from now — only picked up after SendAt passes."""
    days = random.randint(2, 7)
    dt = datetime.now(timezone.utc) + timedelta(days=days, hours=random.randint(8, 17))
    return dt.strftime("%Y-%m-%dT%H:%M:%S+00:00")


def random_phone() -> str:
    return f"+316{random.randint(10000000, 99999999)}"


def post(url: str, tenant_id: str, body: dict) -> tuple[int, str]:
    data = json.dumps(body).encode()
    req = urllib.request.Request(
        url,
        data=data,
        headers={
            "Content-Type": "application/json",
            "X-Tenant-Id": tenant_id,
            "X-Api-Key": API_KEY,
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=5) as resp:
            return resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()
    except Exception as e:
        return 0, str(e)


def color(code: int) -> str:
    if 200 <= code < 300:
        return f"\033[32m{code}\033[0m"
    if 400 <= code < 500:
        return f"\033[33m{code}\033[0m"
    return f"\033[31m{code}\033[0m"


# ── Actions ──────────────────────────────────────────────────────────────────

def send_created(base_url: str, tenant: dict, counter: int) -> dict | None:
    patient_id  = f"PP-{short_id()}"
    appt_id     = f"APT-{short_id()}"
    first       = random.choice(FIRST_NAMES)
    last        = random.choice(LAST_NAMES)
    # Mix: 70% immediate dispatch, 30% future appointment
    scheduled   = scheduled_at_soon() if random.random() < 0.7 else scheduled_at_future()

    body = {
        "action": "CREATED",
        "patient": {
            "externalId": patient_id,
            "givenName": f"{first} {last}",
            "email": f"{first.lower()}.{last.lower().replace(' ', '')}@demo.nl",
            "phoneNumber": random_phone(),
        },
        "appointment": {
            "externalId": appt_id,
            "scheduledAt": scheduled,
            "service": random.choice(SERVICES),
            "location": random.choice(LOCATIONS),
            "instructions": random.choice(INSTRUCTIONS),
        },
    }

    status, _ = post(f"{base_url}/webhooks/appointments", tenant["id"], body)
    print(f"  [{counter:04d}] CREATED  | {tenant['name']:<12} | {patient_id} | HTTP {color(status)}")

    if 200 <= status < 300:
        return {"tenant": tenant, "patient_id": patient_id, "appt_id": appt_id}
    return None


def send_updated(base_url: str, appt: dict, counter: int) -> None:
    body = {
        "action": "UPDATED",
        "patient": {
            "externalId": appt["patient_id"],
            "givenName": f"{random.choice(FIRST_NAMES)} {random.choice(LAST_NAMES)}",
            "email": f"updated{counter}@demo.nl",
            "phoneNumber": random_phone(),
        },
        "appointment": {
            "externalId": appt["appt_id"],
            "scheduledAt": scheduled_at_soon(),
            "service": random.choice(SERVICES),
            "location": random.choice(LOCATIONS),
            "instructions": "Afspraak verzet",
        },
    }

    status, _ = post(f"{base_url}/webhooks/appointments", appt["tenant"]["id"], body)
    print(f"  [{counter:04d}] UPDATED  | {appt['tenant']['name']:<12} | {appt['appt_id']} | HTTP {color(status)}")


def send_cancelled(base_url: str, appt: dict, counter: int) -> None:
    body = {
        "action": "CANCELLED",
        "patient": {
            "externalId": appt["patient_id"],
            "givenName": "N/A",
            "email": None,
            "phoneNumber": None,
        },
        "appointment": {
            "externalId": appt["appt_id"],
            "scheduledAt": scheduled_at_soon(),
            "service": "N/A",
            "location": "N/A",
        },
    }

    status, _ = post(f"{base_url}/webhooks/appointments", appt["tenant"]["id"], body)
    print(f"  [{counter:04d}] CANCELLED| {appt['tenant']['name']:<12} | {appt['appt_id']} | HTTP {color(status)}")


# ── Main loop ─────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--url",      default="http://localhost:8000")
    parser.add_argument("--interval", default="15", help="Seconds between requests, or 'dynamic' for random 1-10s")
    args = parser.parse_args()

    dynamic = args.interval.lower() == "dynamic"
    fixed_interval = None if dynamic else float(args.interval)

    print(f"\033[1mPatientPingeling Demo Generator\033[0m")
    print(f"  API:      {args.url}")
    print(f"  Interval: {'random 1–10s (dynamic)' if dynamic else f'{fixed_interval}s'}")
    print(f"  Tenants:  {', '.join(t['name'] for t in TENANTS)}")
    print(f"  Ctrl+C to stop\n")

    counter = 0
    tenant_idx = 0

    while True:
        counter += 1
        tenant = TENANTS[tenant_idx % len(TENANTS)]
        tenant_idx += 1

        # Every 8th request: CANCEL an existing appointment
        if counter % 8 == 0 and created_appointments:
            appt = created_appointments.pop(random.randrange(len(created_appointments)))
            send_cancelled(args.url, appt, counter)

        # Every 5th request: UPDATE an existing appointment
        elif counter % 5 == 0 and created_appointments:
            appt = random.choice(created_appointments)
            send_updated(args.url, appt, counter)

        # Otherwise: CREATE
        else:
            result = send_created(args.url, tenant, counter)
            if result:
                created_appointments.append(result)
                # Keep the backlog bounded so we don't grow forever
                if len(created_appointments) > 50:
                    created_appointments.pop(0)

        wait = random.uniform(1, 10) if dynamic else fixed_interval
        print(f"         ⏱  next in {wait:.1f}s")
        time.sleep(wait)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\033[33mStopped.\033[0m")
