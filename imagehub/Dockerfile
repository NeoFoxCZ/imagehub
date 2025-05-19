FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["imagehub/imagehub.csproj", "imagehub/"]
RUN dotnet restore "imagehub/imagehub.csproj"
COPY . .
WORKDIR "/src/imagehub"
RUN dotnet build "imagehub.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "imagehub.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

RUN mkdir -p /app/images
COPY --from=publish /app/publish .
RUN chown -R $APP_UID:0 /app && \
    chmod -R 775 /app

USER $APP_UID
ENTRYPOINT ["dotnet", "imagehub.dll"]
