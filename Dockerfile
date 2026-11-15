# Builds and runs the API host (BoardWalk.Api). The bot is a separate process: see BoardWalk.Bot.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Copy project files first so `dotnet restore` is cached until a csproj changes.
COPY ["BoardWalk.Api/BoardWalk.Api.csproj", "BoardWalk.Api/"]
COPY ["BoardWalk.Monopoly/BoardWalk.Monopoly.csproj", "BoardWalk.Monopoly/"]
COPY ["BoardWalk.Monopoly.Contracts/BoardWalk.Monopoly.Contracts.csproj", "BoardWalk.Monopoly.Contracts/"]
RUN dotnet restore "BoardWalk.Api/BoardWalk.Api.csproj"
COPY . .
WORKDIR "/src/BoardWalk.Api"
RUN dotnet build "BoardWalk.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BoardWalk.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BoardWalk.Api.dll"]
