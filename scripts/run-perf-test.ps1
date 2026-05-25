# Runs the k6 load test against a locally running docker compose stack.
# Requires the stack to be up (`docker compose up`) and the API reachable on :8000.
#
# Usage:
#   .\scripts\run-perf-test.ps1
#
# Overrides via environment variables:
#   $env:BASE_URL  = "http://localhost:8000"
#   $env:TENANT_ID = "..."
#   $env:API_KEY   = "..."
#   .\scripts\run-perf-test.ps1

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Resolve-Path (Join-Path $ScriptDir "..")
$TestFile  = Join-Path $RepoRoot "tests\performance\webhook-load.js"

if (-not (Test-Path $TestFile)) {
    Write-Error "Cannot find $TestFile"
    exit 1
}

if (-not $env:BASE_URL)  { $env:BASE_URL  = "http://host.docker.internal:8000" }
if (-not $env:TENANT_ID) { $env:TENANT_ID = "5fa85f64-5717-4562-b3fc-2c963f66afa8" }
if (-not $env:API_KEY)   { $env:API_KEY   = "test-secret" }

Write-Host "-> Running k6 load test against $env:BASE_URL (override with `$env:BASE_URL)"

# host.docker.internal is resolved natively on Docker Desktop for Windows,
# so no --add-host flag is needed here.
$PerfDir = Join-Path $RepoRoot "tests\performance"

docker run --rm -i `
    -v "${PerfDir}:/scripts" `
    -e "BASE_URL=$env:BASE_URL" `
    -e "TENANT_ID=$env:TENANT_ID" `
    -e "API_KEY=$env:API_KEY" `
    grafana/k6:latest run /scripts/webhook-load.js

if ($LASTEXITCODE -ne 0) {
    Write-Warning "k6 exited with code $LASTEXITCODE (threshold breach or runtime error)"
    exit $LASTEXITCODE
}
