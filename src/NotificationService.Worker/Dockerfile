FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy .csproj files first so dotnet restore is cached separately from your source code
COPY NotificationService.Worker/NotificationService.Worker.csproj NotificationService.Worker/
COPY NotificationService.Application/NotificationService.Application.csproj NotificationService.Application/
COPY NotificationService.Infrastructure/NotificationService.Infrastructure.csproj NotificationService.Infrastructure/
COPY NotificationService.Domain/NotificationService.Domain.csproj NotificationService.Domain/
COPY NotificationService.Providers/NotificationService.Providers.csproj NotificationService.Providers/

RUN dotnet restore NotificationService.Worker/NotificationService.Worker.csproj

COPY . .

RUN dotnet publish NotificationService.Worker/NotificationService.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.Worker.dll"]
