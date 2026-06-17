FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EventLedger.Contracts/EventLedger.Contracts.csproj EventLedger.Contracts/
COPY EventGateway/EventGateway.csproj EventGateway/
RUN dotnet restore EventGateway/EventGateway.csproj

COPY EventLedger.Contracts/ EventLedger.Contracts/
COPY EventGateway/ EventGateway/
WORKDIR /src/EventGateway
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EventGateway.dll"]
