# ArcaSim in one image: docker build -t arcasim .
# global.json pins the SDK of the development machine; the image brings its own 8.0 SDK.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY docs/arca/wsdl/ docs/arca/wsdl/
COPY src/ src/
RUN dotnet publish src/ArcaSim.Api -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
# The CA and the token-signing key live here: keep it in a volume, or every restart invalidates cached tickets.
ENV ArcaSim__DataDirectory=/data
VOLUME /data
ENV Urls=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ArcaSim.Api.dll"]
