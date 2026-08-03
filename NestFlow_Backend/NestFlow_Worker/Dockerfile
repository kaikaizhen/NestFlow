FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["NestFlow_Worker/NestFlow_Worker.csproj", "NestFlow_Worker/"]
RUN dotnet restore "./NestFlow_Worker/NestFlow_Worker.csproj"
COPY . .
WORKDIR "/src/NestFlow_Worker"
RUN dotnet build "./NestFlow_Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NestFlow_Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NestFlow_Worker.dll"]
