# Publica eventos de demostracion en campus.events para validar las proyecciones
# del AnalyticsService (dashboard, traza por correlacion, idempotencia).

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $root '.env'

if (-not (Test-Path $envFile)) {
    Write-Error 'No se encontro el archivo .env. Copia .env.example como .env y configura los valores.'
    exit 1
}

$vars = @{}
Get-Content $envFile | Where-Object { $_ -match '^\s*([^#=]+)=(.*)$' } | ForEach-Object {
    $vars[$Matches[1].Trim()] = $Matches[2].Trim()
}

$credentials = '{0}:{1}' -f $vars['RABBITMQ_DEFAULT_USER'], $vars['RABBITMQ_DEFAULT_PASS']
$headers = @{ Authorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($credentials)) }
$publishUri = 'http://localhost:{0}/api/exchanges/%2F/campus.events/publish' -f $vars['RABBITMQ_MANAGEMENT_PORT']

function Publish-CampusEvent {
    param([string]$RoutingKey, [hashtable]$Envelope)
    $body = @{
        properties = @{ content_type = 'application/json'; delivery_mode = 2 }
        routing_key = $RoutingKey
        payload = ($Envelope | ConvertTo-Json -Depth 6 -Compress)
        payload_encoding = 'string'
    } | ConvertTo-Json -Depth 8
    $result = Invoke-RestMethod -Method Post -Uri $publishUri -Headers $headers -ContentType 'application/json' -Body $body
    Write-Output "$($Envelope.eventType) -> enrutado: $($result.routed)"
}

$student = [guid]::NewGuid().ToString()
$correlation = 'corr-demo-{0}' -f (Get-Date -Format 'yyyyMMdd-HHmmss')
$now = (Get-Date).ToUniversalTime().ToString('o')

Write-Output "Correlacion de la demo: $correlation"

$enrolled = @{
    eventId = [guid]::NewGuid().ToString(); eventType = 'StudentEnrolled'; version = 1
    occurredAt = $now; correlationId = $correlation; source = 'AcademicService'; entityId = $student
    data = @{
        studentId = $student; studentCode = 'STU-DEMO'; fullName = 'Estudiante Demo'; grade = '8vo EGB'
        schoolId = 'SCH-001'; schoolYear = '2026-2027'; guardianEmail = 'demo@mail.com'
        enrollmentId = [guid]::NewGuid().ToString()
    }
}
Publish-CampusEvent 'academic.student.enrolled' $enrolled

Publish-CampusEvent 'payments.payment.confirmed' @{
    eventId = [guid]::NewGuid().ToString(); eventType = 'PaymentConfirmed'; version = 1
    occurredAt = $now; correlationId = $correlation; source = 'PaymentService'; entityId = $student
    data = @{
        paymentId = [guid]::NewGuid().ToString(); debtId = [guid]::NewGuid().ToString(); studentId = $student
        concept = 'Matricula 2026-2027'; amount = 350.00; paymentMethod = 'Transferencia'; confirmedAt = $now
    }
}

Publish-CampusEvent 'attendance.record.registered' @{
    eventId = [guid]::NewGuid().ToString(); eventType = 'AttendanceRecorded'; version = 1
    occurredAt = $now; correlationId = $correlation; source = 'AttendanceService'; entityId = $student
    data = @{
        recordId = [guid]::NewGuid().ToString(); studentId = $student; date = (Get-Date -Format 'yyyy-MM-dd')
        status = 'Absent'; remarks = 'Ausencia no justificada'; registeredBy = 'docente'
    }
}

Publish-CampusEvent 'attendance.incident.reported' @{
    eventId = [guid]::NewGuid().ToString(); eventType = 'IncidentReported'; version = 1
    occurredAt = $now; correlationId = $correlation; source = 'AttendanceService'; entityId = $student
    data = @{
        incidentId = [guid]::NewGuid().ToString(); studentId = $student; type = 'Wellbeing'
        severity = 'High'; description = 'Estudiante reporta malestar'; reportedBy = 'docente'
    }
}

Publish-CampusEvent 'notifications.notification.failed' @{
    eventId = [guid]::NewGuid().ToString(); eventType = 'NotificationFailed'; version = 1
    occurredAt = $now; correlationId = $correlation; source = 'NotificationService'; entityId = $student
    data = @{
        notificationId = [guid]::NewGuid().ToString(); sourceEventId = $enrolled.eventId
        sourceEventType = 'StudentEnrolled'; channel = 'Email'; recipient = 'demo@mail.com'
        attempts = 3; failureReason = 'SMTP timeout (simulado)'
    }
}

Write-Output 'Duplicado (mismo eventId de StudentEnrolled, el consumidor debe ignorarlo):'
Publish-CampusEvent 'academic.student.enrolled' $enrolled

Write-Output ''
Write-Output "Verifica el dashboard y la traza con la correlacion $correlation"
