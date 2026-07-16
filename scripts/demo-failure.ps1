# Activa o desactiva la falla simulada de NotificationService a traves del Gateway.
# Uso:  .\scripts\demo-failure.ps1 on    (activa la falla)
#       .\scripts\demo-failure.ps1 off   (la desactiva)
# Con la falla activa, cada notificacion falla, se reintenta 3 veces y cae a la DLQ.

param([ValidateSet('on', 'off')] [string]$estado = 'on')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$gateway = 'http://localhost:8088'

$envFile = Join-Path $root '.env'
$password = 'Demo2026*'
if (Test-Path $envFile) {
    $line = Get-Content $envFile | Where-Object { $_ -match '^AUTH_DEMO_PASSWORD=' }
    if ($line) { $password = ($line -split '=', 2)[1].Trim() }
}

$login = Invoke-RestMethod -Method Post -Uri "$gateway/api/auth/login" -ContentType 'application/json' `
    -Body (@{ username = 'admin'; password = $password } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.accessToken)" }

$enabled = $estado -eq 'on'
$result = Invoke-RestMethod -Method Post -Uri "$gateway/api/notifications/demo/failure" `
    -Headers $headers -ContentType 'application/json' -Body (@{ enabled = $enabled } | ConvertTo-Json)

if ($enabled) {
    Write-Host "`nFalla simulada ACTIVADA." -ForegroundColor Yellow
    Write-Host "Ahora genera una notificacion (registra una ausencia como docente en el portal)."
    Write-Host "Observa: 3 reintentos en Seq, el mensaje cae a notifications.dlq, el dashboard marca Degraded."
    Write-Host "Cuando termines:  .\scripts\demo-failure.ps1 off"
}
else {
    Write-Host "`nFalla simulada DESACTIVADA. Las notificaciones vuelven a procesarse." -ForegroundColor Green
    Write-Host "Reprocesa las fallidas desde el dashboard o con POST /api/notifications/{id}/retry."
}
