FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY KioraRestaurante.csproj ./

RUN dotnet restore

COPY . .

RUN dotnet publish KioraRestaurante.csproj -c Release -o /app/publish --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "KioraRestaurante.dll"]