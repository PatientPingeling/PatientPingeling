FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy .csproj files first so dotnet restore is cached separately from your source code
COPY NotificationService.Scheduler/NotificationService.Scheduler.csproj NotificationService.Scheduler/
COPY NotificationService.Application/NotificationService.Application.csproj NotificationService.Application/
COPY NotificationService.Infrastructure/NotificationService.Infrastructure.csproj NotificationService.Infrastructure/
COPY NotificationService.Domain/NotificationService.Domain.csproj NotificationService.Domain/

RUN dotnet restore NotificationService.Scheduler/NotificationService.Scheduler.csproj

COPY . .

RUN dotnet publish NotificationService.Scheduler/NotificationService.Scheduler.csproj -c Release -o /app/publish --no-restore

# The scheduler references the ASP.NET shared framework (Microsoft.AspNetCore.App)
# so it must run on the aspnet image (not the plain runtime image).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.Scheduler.dll"]
