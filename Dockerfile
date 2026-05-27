FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Marketplace/Marketplace.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/data
EXPOSE 8080
ENTRYPOINT ["dotnet", "Marketplace.dll"]
