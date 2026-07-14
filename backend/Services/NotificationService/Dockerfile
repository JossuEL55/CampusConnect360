# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CampusConnect360.slnx ./

COPY backend/Services/NotificationService/NotificationService.csproj backend/Services/NotificationService/
COPY backend/SharedKernel/SharedKernel.csproj backend/SharedKernel/

RUN dotnet restore backend/Services/NotificationService/NotificationService.csproj

COPY backend/Services/NotificationService/ backend/Services/NotificationService/
COPY backend/SharedKernel/ backend/SharedKernel/

RUN dotnet publish backend/Services/NotificationService/NotificationService.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "NotificationService.dll"]
