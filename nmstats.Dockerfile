FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-preview-noble-aot AS restore
ARG TARGETARCH
ENV RUNTIME_IDENTIFIER=linux-${TARGETARCH}
WORKDIR /build

COPY ["src/BUTR.NexusModsStats/BUTR.NexusModsStats.csproj", "src/BUTR.NexusModsStats/"]
COPY ["src/nuget.config", "src/"]

RUN dotnet restore "src/BUTR.NexusModsStats/BUTR.NexusModsStats.csproj" -r $RUNTIME_IDENTIFIER;

COPY ["src/BUTR.NexusModsStats/", "src/BUTR.NexusModsStats/"]


FROM restore AS publish
ARG TARGETARCH
ENV RUNTIME_IDENTIFIER=linux-${TARGETARCH}
WORKDIR /build

RUN apt-get update && apt-get install -y upx-ucl && rm -rf /var/lib/apt/lists/*

RUN dotnet publish "src/BUTR.NexusModsStats/BUTR.NexusModsStats.csproj" -c Release -r $RUNTIME_IDENTIFIER -o /app/publish;

RUN upx --best --lzma /app/publish/BUTR.NexusModsStats || echo "UPX failed, continuing"

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-noble-chiseled AS final
WORKDIR /app

COPY --from=publish /app/publish /app

USER $APP_UID

LABEL org.opencontainers.image.source="https://github.com/BUTR/BUTR.NexusModsStats"
EXPOSE 8080/tcp

ENTRYPOINT ["/app/BUTR.NexusModsStats"]