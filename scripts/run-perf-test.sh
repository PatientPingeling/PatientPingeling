#!/usr/bin/env bash
# Runs the k6 load test against a locally running docker compose stack.
# Requires the stack to be up (`docker compose up`) and the API reachable on :8000.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEST_FILE="$REPO_ROOT/tests/performance/webhook-load.js"

if [[ ! -f "$TEST_FILE" ]]; then
    echo "Cannot find $TEST_FILE" >&2
    exit 1
fi

BASE_URL="${BASE_URL:-http://host.docker.internal:8000}"
echo "→ Running k6 load test against $BASE_URL (override with BASE_URL env var)"

# --add-host lets the container resolve host.docker.internal on Linux too.
# We mount the script in and run k6 against the host's exposed API port.
docker run --rm -i \
    --add-host=host.docker.internal:host-gateway \
    -v "$REPO_ROOT/tests/performance:/scripts" \
    -e "BASE_URL=$BASE_URL" \
    -e "TENANT_ID=${TENANT_ID:-3fa85f64-5717-4562-b3fc-2c963f66afa6}" \
    -e "API_KEY=${API_KEY:-test-secret}" \
    grafana/k6:latest run /scripts/webhook-load.js
