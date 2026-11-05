FROM debian:bookworm-slim AS simulator
RUN apt-get update && apt-get install -y --no-install-recommends g++ cmake make pkg-config libmosquitto-dev ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /simulation
COPY simulation .
RUN cmake -S . -B build -DCMAKE_BUILD_TYPE=Release && cmake --build build -j2 && ctest --test-dir build --output-on-failure
FROM mcr.microsoft.com/dotnet/sdk:10.0.100 AS build
WORKDIR /src
COPY backend/Backend.csproj .
RUN dotnet restore
COPY backend .
RUN dotnet publish -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0
RUN apt-get update && apt-get install -y --no-install-recommends curl libmosquitto1 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
COPY --from=simulator /simulation/build/simulation /app/simulation
COPY simulation/scenarios /app/scenarios
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Backend.dll"]
