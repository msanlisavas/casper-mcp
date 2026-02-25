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

# API key can be passed as argument or environment variable
ENV CSPR_CLOUD_API_KEY=""
ENV CASPER_MCP_SERVER_API_KEY=""
ENV CASPER_MCP_TRANSPORT="stdio"
ENV CASPER_MCP_PORT="3001"

EXPOSE 3001

ENTRYPOINT ["dotnet", "CasperMcp.dll"]
