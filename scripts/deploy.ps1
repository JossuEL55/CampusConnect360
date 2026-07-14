# Levanta el ecosistema CampusConnect360 con Docker Compose.

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "`n=== CampusConnect360: despliegue local ===" -ForegroundColor Cyan

if (-not (Test-Path ".env")) {
    Write-Error "No se encontró el archivo .env. Copia .env.example como .env y configura los valores."
    exit 1
}

Write-Host "Validando Docker..."
$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "SilentlyContinue"
docker info *> $null
$dockerInfoExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference

if ($dockerInfoExitCode -ne 0) {
    Write-Error "Docker Desktop no está disponible o el motor no está iniciado."
    exit 1
}

Write-Host "Validando docker-compose.yml..."
docker compose config --quiet

if ($LASTEXITCODE -ne 0) {
    Write-Error "La configuración de Docker Compose contiene errores."
    exit 1
}

Write-Host "Construyendo y levantando contenedores..."
docker compose up -d --build

if ($LASTEXITCODE -ne 0) {
    Write-Error "No fue posible levantar el ecosistema."
    exit 1
}

Write-Host "`nEsperando el inicio de los servicios..."
Start-Sleep -Seconds 10

Write-Host "`nEstado de los contenedores:" -ForegroundColor Cyan
docker compose ps

Write-Host "`nVerificando Gateway y microservicios..."

try {
    $health = Invoke-RestMethod `
        -Uri "http://localhost:8088/health/services" `
        -TimeoutSec 15

    Write-Host "`nEstado general: $($health.status)" -ForegroundColor Green
    Write-Host "Servicios saludables: $($health.healthyServices)/$($health.totalServices)"
}
catch {
    Write-Warning "Los contenedores iniciaron, pero el Gateway todavía no responde."
    Write-Warning "Espera unos segundos y ejecuta: .\scripts\test-gateway-routes.ps1"
}

Write-Host "`nAccesos disponibles:"
Write-Host "Gateway:   http://localhost:8088"
Write-Host "RabbitMQ:  http://localhost:15672"
Write-Host "Seq:       http://localhost:8081"
