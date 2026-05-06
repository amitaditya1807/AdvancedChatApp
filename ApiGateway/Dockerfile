# ---------- BUILD STAGE ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy only gateway project (important for monorepo)
COPY ApiGateway/ApiGateway.csproj ./ApiGateway/
RUN dotnet restore ./ApiGateway/ApiGateway.csproj

# copy full gateway code
COPY ApiGateway/. ./ApiGateway/

WORKDIR /src/ApiGateway
RUN dotnet publish -c Release -o /app/publish

# ---------- RUNTIME STAGE ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:10000

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ApiGateway.dll"]