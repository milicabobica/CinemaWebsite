FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CinemaWebsite/CinemaWebsite.csproj CinemaWebsite/
RUN dotnet restore CinemaWebsite/CinemaWebsite.csproj

COPY CinemaWebsite/ CinemaWebsite/
RUN dotnet publish CinemaWebsite/CinemaWebsite.csproj \
    --configuration Release --no-restore --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

RUN mkdir -p /app/data && chown app:app /app/data
COPY --from=build /app/publish .

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "CinemaWebsite.dll"]
