FROM rust AS compiler
WORKDIR /src/SpacemanDMM
COPY SpacemanDMM/ ./
RUN cargo build -p cli --release

FROM microsoft/aspnetcore-build:2.0.6-2.1.101-jessie AS base
FROM base as build

WORKDIR /src/SpacemanDMM/target/release/
COPY --from=compiler /src/SpacemanDMM/target/release/dmm-tools.exe .

WORKDIR /src
COPY MapDiffBot/* MapDiffBot/
COPY MapDiffBot.Tests/* MapDiffBot.Tests/

RUN dotnet restore MapDiffBot.Tests/MapDiffBot.Tests.csproj
WORKDIR /src/MapDiffBot.Tests
RUN dotnet test -c Docker

FROM build AS publish
WORKDIR /src/MapDiffBot
RUN dotnet publish -c Docker -o /app
RUN cp entrypoint.sh /app/

FROM base AS final
WORKDIR /app
COPY --from=publish /app .

RUN mkdir /config_data
RUN mv appsettings.Docker.json /config_data/appsettings.Production.json
VOLUME ["/config_data"]

EXPOSE 80
ENTRYPOINT ["entrypoint.sh"]
