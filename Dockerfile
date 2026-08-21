FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ARG VERSION=0.0.0-dev

COPY . .
RUN dotnet restore MediaFlow.sln
RUN dotnet publish src/MediaFlow.Web/MediaFlow.Web.csproj \
    --configuration Release \
    --no-restore \
    -p:Version="$VERSION" \
    -p:InformationalVersion="$VERSION" \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libimage-exiftool-perl curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent --show-error http://localhost:8080/health >/dev/null || exit 1

ENTRYPOINT ["dotnet", "MediaFlow.Web.dll"]
