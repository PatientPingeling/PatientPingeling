FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy .csproj files first so dotnet restore is cached separately from your source code
COPY NotificationService.Listener/NotificationService.Listener.csproj NotificationService.Listener/
COPY NotificationService.Application/NotificationService.Application.csproj NotificationService.Application/
COPY NotificationService.Infrastructure/NotificationService.Infrastructure.csproj NotificationService.Infrastructure/
COPY NotificationService.Domain/NotificationService.Domain.csproj NotificationService.Domain/

RUN dotnet restore NotificationService.Listener/NotificationService.Listener.csproj

COPY . .

RUN dotnet publish NotificationService.Listener/NotificationService.Listener.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.Listener.dll"]
