FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY shared/ shared/
COPY gateways/admin-bff/ gateways/admin-bff/
WORKDIR /src/gateways/admin-bff
RUN dotnet publish AdminBff.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS="http://0.0.0.0:8080"
EXPOSE 8080
ENTRYPOINT ["dotnet", "AdminBff.dll"]
