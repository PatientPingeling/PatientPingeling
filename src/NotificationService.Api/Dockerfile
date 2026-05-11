FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NotificationService.Api/NotificationService.Api.csproj NotificationService.Api/
COPY NotificationService.Application/NotificationService.Application.csproj NotificationService.Application/
COPY NotificationService.Infrastructure/NotificationService.Infrastructure.csproj NotificationService.Infrastructure/
COPY NotificationService.Domain/NotificationService.Domain.csproj NotificationService.Domain/
COPY NotificationService.Providers/NotificationService.Providers.csproj NotificationService.Providers/

RUN dotnet restore NotificationService.Api/NotificationService.Api.csproj

COPY . .

RUN dotnet publish NotificationService.Api/NotificationService.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.Api.dll"]
