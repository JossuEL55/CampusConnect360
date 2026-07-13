# Detiene los contenedores sin eliminar los volúmenes persistentes.

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "`nDeteniendo CampusConnect360..." -ForegroundColor Yellow

docker compose down

if ($LASTEXITCODE -ne 0) {
    Write-Error "No fue posible detener los contenedores."
    exit 1
}

Write-Host "Ecosistema detenido correctamente." -ForegroundColor Green
Write-Host "Los volúmenes y datos persistentes se conservaron."