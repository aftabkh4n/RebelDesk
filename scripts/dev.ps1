# Development launcher — starts the server, waits for /healthz, and prints instructions
# for the agent and browser client. Uses non-persistent state under ./data/.

param(
    [switch]$WithClient,
    [switch]$WithAgent
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$data = Join-Path $repo "data"
if (-not (Test-Path $data)) { New-Item -ItemType Directory -Path $data | Out-Null }

Write-Host "[dev] Starting server on http://localhost:8080..." -ForegroundColor Cyan
$server = Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--project", (Join-Path $repo "server\RebelDesk.Server.csproj"),
                    "--Database:Path=$(Join-Path $data 'rebeldesk.db')") `
    -PassThru -WindowStyle Normal

Start-Sleep -Seconds 6
try { Invoke-RestMethod http://localhost:8080/healthz | Out-Null; Write-Host "[dev] Server healthy." -ForegroundColor Green }
catch { Write-Warning "[dev] Server did not become healthy. Check its window."; return }

if ($WithClient) {
    Write-Host "[dev] Starting client on http://localhost:5173..." -ForegroundColor Cyan
    Start-Process -FilePath "npm" -ArgumentList @("--prefix", (Join-Path $repo "client"), "run", "dev") `
        -WindowStyle Normal | Out-Null
}

if ($WithAgent) {
    Write-Host "[dev] Starting agent (attach to server console for pairing input)..." -ForegroundColor Cyan
    Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", (Join-Path $repo "agent\RebelDesk.Agent.csproj")) `
        -WindowStyle Normal | Out-Null
}

Write-Host ""
Write-Host "[dev] Server PID: $($server.Id). Kill it with:  Stop-Process $($server.Id)"
Write-Host "[dev] Browser: http://localhost:5173"
Write-Host "[dev] To run the agent:  dotnet run --project agent/RebelDesk.Agent.csproj"
