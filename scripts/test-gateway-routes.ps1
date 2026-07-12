# Comprueba que las cinco rutas de YARP respondan correctamente.

$gatewayUrl = "http://localhost:8088"

$routes = @(
    @{
        Name = "AcademicService"
        Path = "/api/academic/health"
        CorrelationId = "demo-yarp-academic-001"
    },
    @{
        Name = "PaymentService"
        Path = "/api/payments/health"
        CorrelationId = "demo-yarp-payments-001"
    },
    @{
        Name = "AttendanceService"
        Path = "/api/attendance/health"
        CorrelationId = "demo-yarp-attendance-001"
    },
    @{
        Name = "NotificationService"
        Path = "/api/notifications/health"
        CorrelationId = "demo-yarp-notifications-001"
    },
    @{
        Name = "AnalyticsService"
        Path = "/api/analytics/health"
        CorrelationId = "demo-yarp-analytics-001"
    }
)

$results = foreach ($route in $routes) {
    $uri = "$gatewayUrl$($route.Path)"

    try {
        $response = Invoke-RestMethod `
            -Uri $uri `
            -Headers @{
                "X-Correlation-Id" = $route.CorrelationId
            }

        [PSCustomObject]@{
            Route = $route.Path
            ExpectedService = $route.Name
            RespondedService = $response.service
            Status = $response.status
            CorrelationId = $response.correlationId
            Result = if (
                $response.service -eq $route.Name -and
                $response.status -eq "Healthy" -and
                $response.correlationId -eq $route.CorrelationId
            ) {
                "OK"
            }
            else {
                "INVALID"
            }
        }
    }
    catch {
        [PSCustomObject]@{
            Route = $route.Path
            ExpectedService = $route.Name
            RespondedService = "-"
            Status = "Unavailable"
            CorrelationId = $route.CorrelationId
            Result = "ERROR"
        }
    }
}

$results | Format-Table -AutoSize

if ($results.Result -contains "ERROR" -or
    $results.Result -contains "INVALID") {
    exit 1
}

Write-Host "`nTodas las rutas del Gateway funcionan correctamente."