FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EventLedger.Contracts/EventLedger.Contracts.csproj EventLedger.Contracts/
COPY AccountService/AccountService.csproj AccountService/
RUN dotnet restore AccountService/AccountService.csproj

COPY EventLedger.Contracts/ EventLedger.Contracts/
COPY AccountService/ AccountService/
WORKDIR /src/AccountService
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8081
ENV ASPNETCORE_URLS=http://+:8081
ENTRYPOINT ["dotnet", "AccountService.dll"]
