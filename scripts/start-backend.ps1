# Levanta Gateway y los cinco microservicios en terminales independientes.

$root = Split-Path -Parent $PSScriptRoot

$projects = @(
    @{
        Name = "AcademicService"
        Path = "backend/Services/AcademicService/AcademicService.csproj"
    },
    @{
        Name = "PaymentService"
        Path = "backend/Services/PaymentService/PaymentService.csproj"
    },
    @{
        Name = "AttendanceService"
        Path = "backend/Services/AttendanceService/AttendanceService.csproj"
    },
    @{
        Name = "NotificationService"
        Path = "backend/Services/NotificationService/NotificationService.csproj"
    },
    @{
        Name = "AnalyticsService"
        Path = "backend/Services/AnalyticsService/AnalyticsService.csproj"
    },
    @{
        Name = "Gateway"
        Path = "backend/Gateway/Gateway.csproj"
    }
)

foreach ($project in $projects) {
    $projectPath = Join-Path $root $project.Path

    Start-Process powershell.exe -ArgumentList @(
        "-NoExit",
        "-Command",
        "Set-Location '$root'; Write-Host 'Iniciando $($project.Name)...'; dotnet run --project '$projectPath'"
    )
}

Write-Host "Gateway y microservicios iniciados en terminales independientes."