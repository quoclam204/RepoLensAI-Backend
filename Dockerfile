# Stage 1: Build source code
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution config and project files to leverage Docker caching
COPY Directory.Build.props ./
COPY src/RepoLens.Domain/*.csproj src/RepoLens.Domain/
COPY src/RepoLens.Application/*.csproj src/RepoLens.Application/
COPY src/RepoLens.Analysis/*.csproj src/RepoLens.Analysis/
COPY src/RepoLens.Infrastructure/*.csproj src/RepoLens.Infrastructure/
COPY src/RepoLens.Api/*.csproj src/RepoLens.Api/

RUN dotnet restore src/RepoLens.Api/RepoLens.Api.csproj

# Copy the rest of the source code and publish
COPY src/ src/
WORKDIR /source/src/RepoLens.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install git (required for repository acquisition)
RUN apt-get update && apt-get install -y git && rm -rf /var/lib/apt/lists/*

# Temporary workspace directory for cloned repositories
RUN mkdir -p /tmp/repolens-workspaces

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV Workspace__BaseDirectory=/tmp/repolens-workspaces
EXPOSE 8080

ENTRYPOINT ["dotnet", "RepoLens.Api.dll"]
