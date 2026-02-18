FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["KuechenRezepte.csproj", "./"]
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/wwwroot/uploads

ENV ASPNETCORE_URLS=http://+:6655
EXPOSE 6655

ENTRYPOINT ["dotnet", "KuechenRezepte.dll"]
