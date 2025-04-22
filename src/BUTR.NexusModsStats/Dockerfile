FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

RUN apk add --no-cache \
    clang lld gcc g++ musl-dev zlib-dev libgcc libstdc++ binutils upx

WORKDIR /src
COPY ["BUTR.NexusModsStats/BUTR.NexusModsStats.csproj", "BUTR.NexusModsStats/"]
RUN dotnet restore "BUTR.NexusModsStats/BUTR.NexusModsStats.csproj"
COPY . .
WORKDIR "/src/BUTR.NexusModsStats"

RUN chmod +x build.sh && ./build.sh

RUN upx --best --lzma /app/publish/BUTR.NexusModsStats || echo "UPX failed, continuing"

FROM alpine:3.19 AS final
WORKDIR /app

# Optional: Add busybox so you can exec into the container for debugging
RUN apk add --no-cache busybox

COPY --from=build /app/publish /app
RUN chmod +x /app/BUTR.NexusModsStats

EXPOSE 5000
EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["/app/BUTR.NexusModsStats"]