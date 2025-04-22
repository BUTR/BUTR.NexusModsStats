FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-preview-alpine-aot AS restore
ARG TARGETARCH
ENV RUNTIME_IDENTIFIER=linux-musl-${TARGETARCH}
WORKDIR /build

COPY ["src/BUTR.NexusModsStats/BUTR.NexusModsStats.csproj", "src/BUTR.NexusModsStats/"]
COPY ["src/nuget.config", "src/"]

RUN dotnet restore "src/BUTR.NexusModsStats/BUTR.NexusModsStats.csproj" -r $RUNTIME_IDENTIFIER;

COPY ["src/BUTR.NexusModsStats/", "src/BUTR.NexusModsStats/"]


FROM restore AS publish
ARG TARGETARCH
ENV RUNTIME_IDENTIFIER=linux-musl-${TARGETARCH}
WORKDIR /build

RUN apk add --no-cache upx

RUN dotnet publish "src/BUTR.NexusModsStats/BUTR.NexusModsStats.csproj" -c Release -r $RUNTIME_IDENTIFIER -o /app/publish;

RUN upx --best --lzma /app/publish/BUTR.NexusModsStats || echo "UPX failed, continuing"

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine AS final
WORKDIR /app

COPY --from=publish /app/publish /app

USER $APP_UID

LABEL org.opencontainers.image.source="https://github.com/BUTR/BUTR.NexusModsStats"
EXPOSE 8080/tcp

ENTRYPOINT ["/app/BUTR.NexusModsStats"]