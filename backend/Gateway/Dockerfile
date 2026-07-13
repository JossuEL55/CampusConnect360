# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CampusConnect360.slnx ./

COPY backend/Gateway/Gateway.csproj backend/Gateway/
COPY backend/SharedKernel/SharedKernel.csproj backend/SharedKernel/

RUN dotnet restore backend/Gateway/Gateway.csproj

COPY backend/Gateway/ backend/Gateway/
COPY backend/SharedKernel/ backend/SharedKernel/

RUN dotnet publish backend/Gateway/Gateway.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Gateway.dll"]