FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "global.json", "./"]
COPY ["src/", "src/"]
RUN dotnet publish src/Marketplace.Api/Marketplace.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
RUN adduser -D -u 1000 app && mkdir -p /app && chown app:app /app
WORKDIR /app
COPY --from=build --chown=app:app /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Marketplace.Api.dll"]
