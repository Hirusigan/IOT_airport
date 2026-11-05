FROM debian:bookworm-slim AS publisher
RUN apt-get update && apt-get install -y --no-install-recommends g++ cmake make libmosquitto-dev nlohmann-json3-dev && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY publisher .
RUN cmake -S . -B build -DCMAKE_BUILD_TYPE=Release && cmake --build build -j2
FROM mcr.microsoft.com/dotnet/sdk:10.0.100 AS backend
WORKDIR /src
COPY backend/Showcase.csproj .
RUN dotnet restore
COPY backend .
RUN dotnet publish -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0
RUN apt-get update && apt-get install -y --no-install-recommends curl libmosquitto1 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=backend /out .
COPY --from=publisher /src/build/publisher /app/publisher
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Showcase.dll"]
