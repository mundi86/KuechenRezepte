FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["KuechenRezepte.csproj", "./"]
RUN dotnet restore

COPY . .
RUN dotnet publish KuechenRezepte.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

RUN useradd --create-home --home-dir /home/appuser --shell /usr/sbin/nologin --uid 10001 --user-group appuser \
    && mkdir -p /app/wwwroot/uploads /home/appuser/.aspnet/DataProtection-Keys \
    && chown -R appuser:appuser /app /home/appuser

USER appuser

ENV HOME=/home/appuser
ENV ASPNETCORE_URLS=http://+:6655
EXPOSE 6655

ENTRYPOINT ["dotnet", "KuechenRezepte.dll"]
