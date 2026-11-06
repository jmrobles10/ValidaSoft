FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilacion
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props Validasoft.slnx ./
COPY src/ src/
RUN dotnet publish src/Validasoft.Api/Validasoft.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=compilacion /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Validasoft.Api.dll"]
