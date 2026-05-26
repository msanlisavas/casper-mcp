FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY casper-mcp.slnx .
COPY src/CasperMcp/CasperMcp.csproj src/CasperMcp/
COPY tests/CasperMcp.Tests/CasperMcp.Tests.csproj tests/CasperMcp.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/CasperMcp/CasperMcp.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app/publish .

# Auth and transport config — pass via docker-compose.yml or CLI args
ENV CASPER_MCP_AUTH_MODE=""
ENV CASPER_MCP_AUTH_API_KEY=""
ENV CASPER_MCP_AUTH_JWT_AUTHORITY=""
ENV CASPER_MCP_AUTH_JWT_AUDIENCE=""
ENV CASPER_MCP_PORT="3001"

EXPOSE 3001

ENTRYPOINT ["dotnet", "CasperMcp.dll"]
