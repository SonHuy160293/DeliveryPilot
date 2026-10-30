FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-base
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props DeliveryPilot.slnx .editorconfig ./
COPY src/ ./src/

FROM build-base AS identity-build
RUN dotnet restore src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API/DeliveryPilot.Identity.API.csproj
RUN dotnet restore src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.MigrationRunner/DeliveryPilot.Identity.MigrationRunner.csproj
RUN dotnet publish src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.API/DeliveryPilot.Identity.API.csproj -c Release -o /app/identity-api --no-restore /p:UseAppHost=false
RUN dotnet publish src/Modules/DeliveryPilot.Identity/DeliveryPilot.Identity.MigrationRunner/DeliveryPilot.Identity.MigrationRunner.csproj -c Release -o /app/identity-migration --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api-runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

FROM api-runtime AS final-identity-api
COPY --from=identity-build /app/identity-api .
ENTRYPOINT ["dotnet", "DeliveryPilot.Identity.API.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final-identity-migration
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=identity-build /app/identity-migration .
USER $APP_UID
ENTRYPOINT ["dotnet", "DeliveryPilot.Identity.MigrationRunner.dll"]

# Default image serves the Identity API.
FROM final-identity-api AS final-api
