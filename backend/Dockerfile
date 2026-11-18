FROM mcr.microsoft.com/dotnet/sdk:10.0.100 AS build
WORKDIR /src
COPY backend/Backend.csproj ./backend/
RUN dotnet restore ./backend/Backend.csproj
COPY backend ./backend
RUN dotnet publish ./backend/Backend.csproj -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0
RUN apt-get update && apt-get install -y --no-install-recommends curl mosquitto-clients && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Backend.dll"]
