FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS restore
ARG TARGETARCH
WORKDIR /build

COPY ["src/BUTR.NexusModsStats/BUTR.NexusModsStats.csproj", "src/BUTR.NexusModsStats/"]
COPY ["src/nuget.config", "src/"]

RUN dotnet restore "src/BUTR.NexusModsStats/BUTR.NexusModsStats.csproj" -a $TARGETARCH;

COPY ["src/BUTR.NexusModsStats/", "src/BUTR.NexusModsStats/"]


FROM restore AS publish
ARG TARGETARCH
WORKDIR /build

RUN apk add --no-cache \
    clang lld gcc g++ musl-dev zlib-dev libgcc libstdc++ binutils upx

RUN dotnet publish "src/BUTR.NexusModsStats/BUTR.NexusModsStats.csproj" -c Release -a $TARGETARCH -o /app/publish;

# RUN chmod +x build.sh && ./build.sh

RUN upx --best --lzma /app/publish/BUTR.NexusModsStats || echo "UPX failed, continuing"

FROM alpine:3.19 AS final
WORKDIR /app

COPY --from=publish /app/publish /app
RUN chmod +x /app/BUTR.NexusModsStats

LABEL org.opencontainers.image.source="https://github.com/BUTR/BUTR.NexusModsStats"
EXPOSE 8080/tcp

ENTRYPOINT ["/app/BUTR.NexusModsStats"]