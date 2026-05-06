FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for layer-cached restore
COPY src/NotificationService.Api/NotificationService.Api.csproj             src/NotificationService.Api/
COPY src/NotificationService.Core/NotificationService.Core.csproj           src/NotificationService.Core/
COPY src/NotificationService.Infrastructure/NotificationService.Infrastructure.csproj src/NotificationService.Infrastructure/

RUN dotnet restore src/NotificationService.Api/NotificationService.Api.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/NotificationService.Api/NotificationService.Api.csproj \
    --no-restore \
    --configuration Release \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

EXPOSE 8080

RUN addgroup --system appgroup && \
    adduser --system --ingroup appgroup appuser

COPY --from=build /app/publish .

RUN chown -R appuser:appgroup /app

USER appuser

ENTRYPOINT ["dotnet", "NotificationService.Api.dll"]
