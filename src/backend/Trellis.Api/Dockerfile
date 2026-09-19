FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/backend/Directory.Build.props src/backend/
COPY src/backend/Trellis.Api/Trellis.Api.csproj src/backend/Trellis.Api/
RUN dotnet restore src/backend/Trellis.Api/Trellis.Api.csproj

COPY src/backend/Trellis.Api/ src/backend/Trellis.Api/
RUN dotnet publish src/backend/Trellis.Api/Trellis.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Trellis.Api.dll"]
