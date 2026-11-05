FROM mcr.microsoft.com/dotnet/sdk:10.0.100
WORKDIR /src
COPY backend backend
COPY backend-tests backend-tests
RUN dotnet build backend-tests/Tests.csproj -c Release
ENTRYPOINT ["dotnet", "test", "backend-tests/Tests.csproj", "-c", "Release", "--no-build"]
