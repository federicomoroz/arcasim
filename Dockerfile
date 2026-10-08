# ArcaSim in one image: docker build -t arcasim .
# global.json pins the SDK of the development machine; the image brings its own 8.0 SDK,
# and the CI starts the image it built and calls it before publishing it.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# The project files first: the packages restore in a layer of their own, reused until a project changes.
COPY Directory.Build.props ./
COPY src/ArcaSim.Domain/ArcaSim.Domain.csproj src/ArcaSim.Domain/
COPY src/ArcaSim.Application/ArcaSim.Application.csproj src/ArcaSim.Application/
COPY src/ArcaSim.Infrastructure/ArcaSim.Infrastructure.csproj src/ArcaSim.Infrastructure/
COPY src/ArcaSim.Api/ArcaSim.Api.csproj src/ArcaSim.Api/
RUN dotnet restore src/ArcaSim.Api/ArcaSim.Api.csproj
COPY docs/arca/wsdl/ docs/arca/wsdl/
COPY docs/arca/servicios.json docs/arca/
COPY src/ src/
RUN dotnet publish src/ArcaSim.Api -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
# The CA and the token-signing key live here: keep it in a volume, or every restart invalidates cached tickets.
# It runs as root so that a volume an earlier image created, owned by root, stays writable.
ENV ArcaSim__DataDirectory=/data
VOLUME /data
ENV Urls=http://+:8080
EXPOSE 8080
# The image has no curl: bash asks for WSFEv1's help page through /dev/tcp.
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /wsfev1/service.asmx HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && head -n 1 <&3 | grep -q ' 200 '"]
ENTRYPOINT ["dotnet", "ArcaSim.Api.dll"]
