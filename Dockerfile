FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY casper-mcp.slnx .
COPY src/CasperMcp/CasperMcp.csproj src/CasperMcp/
COPY tests/CasperMcp.Tests/CasperMcp.Tests.csproj tests/CasperMcp.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/CasperMcp/CasperMcp.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Transport, auth, and credentials are supplied at runtime via CLI args or environment
# (e.g. --transport http --port 3001 --auth-mode apikey, or CASPER_MCP_AUTH_* / CSPR_CLOUD_API_KEY).
# See README.md and docker-compose.yml. Nothing secret is baked into the image.

EXPOSE 3001

ENTRYPOINT ["dotnet", "CasperMcp.dll"]
