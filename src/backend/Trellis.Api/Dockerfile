# Build context is src/ (this repository's own root once extracted — see
# project_architecture.md's Repository Structure): docker build -f backend/Trellis.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/Directory.Build.props backend/
COPY backend/Trellis.Api/Trellis.Api.csproj backend/Trellis.Api/
RUN dotnet restore backend/Trellis.Api/Trellis.Api.csproj

COPY backend/Trellis.Api/ backend/Trellis.Api/
RUN dotnet publish backend/Trellis.Api/Trellis.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Trellis.Api.dll"]
