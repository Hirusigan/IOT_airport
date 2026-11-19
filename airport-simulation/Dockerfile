FROM mcr.microsoft.com/dotnet/sdk:10.0.100 AS build
WORKDIR /src
COPY airport-simulation/*.csproj ./
RUN dotnet restore
COPY airport-simulation/ ./
RUN dotnet publish -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0
WORKDIR /app
COPY --from=build /out .
COPY scenarios ./scenarios
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "AirportSimulation.dll"]
