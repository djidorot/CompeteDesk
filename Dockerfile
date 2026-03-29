# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY CompeteDesk.sln ./
COPY CompeteDesk.Web/CompeteDesk.csproj CompeteDesk.Web/
RUN dotnet restore CompeteDesk.Web/CompeteDesk.csproj

COPY . ./
WORKDIR /src/CompeteDesk.Web
RUN dotnet publish CompeteDesk.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

ENTRYPOINT ["dotnet", "CompeteDesk.dll"]