# Elimina contenedores, volúmenes y reconstruye el ecosistema desde cero.
# ADVERTENCIA: borra los datos locales de PostgreSQL, RabbitMQ y Seq.

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$confirmation = Read-Host "Este proceso eliminará todos los datos locales. Escribe RESET para continuar"

if ($confirmation -ne "RESET") {
    Write-Host "Operación cancelada."
    exit 0
}

docker compose down -v --remove-orphans

if ($LASTEXITCODE -ne 0) {
    Write-Error "No fue posible limpiar el entorno."
    exit 1
}

Write-Host "Entorno eliminado. Ejecuta .\scripts\deploy.ps1 para reconstruirlo."