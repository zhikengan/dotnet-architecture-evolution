FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY shared/ shared/
COPY gateways/buyer-bff/ gateways/buyer-bff/
WORKDIR /src/gateways/buyer-bff
RUN dotnet publish BuyerBff.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS="http://0.0.0.0:8080"
EXPOSE 8080
ENTRYPOINT ["dotnet", "BuyerBff.dll"]
