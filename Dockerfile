FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "global.json", "./"]
COPY ["src/", "src/"]
RUN dotnet publish src/Marketplace.Api/Marketplace.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
# The base image ships with a non-root 'app' user; reuse it.
WORKDIR /app
COPY --from=build --chown=app:app /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Marketplace.Api.dll"]
