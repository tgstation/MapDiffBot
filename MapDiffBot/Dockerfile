FROM rust AS compiler
WORKDIR /src/SpacemanDMM
COPY SpacemanDMM/ ./
RUN cargo build -p cli --release

FROM microsoft/aspnetcore-build:2.0.6-2.1.101-jessie AS build

WORKDIR /src
COPY MapDiffBot/* MapDiffBot/
COPY MapDiffBot.Tests/* MapDiffBot.Tests/

WORKDIR /src/MapDiffBot.Tests
RUN dotnet restore
RUN dotnet test -c Docker

WORKDIR /src/MapDiffBot
RUN dotnet publish -c Docker -o /app && cp entrypoint.sh /app/

FROM microsoft/aspnetcore:2.0.6-jessie AS base

#deps for libgit2
RUN apt-get update && apt-get install -y --no-install-recommends \
	libc6 \
	zlib1g-dev \
	libcomerr2 \
	libc6-dev \
	libgcrypt20 \
	libkeyutils1 \
	libcurl3-gnutls \
	libsasl2-2 \
	libgpg-error0 \
	&& rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .
COPY --from=compiler /src/SpacemanDMM/target/release/dmm-tools dmm-tools.exe

RUN mkdir /config_data && mv appsettings.Docker.json /config_data/appsettings.Production.json
VOLUME ["/config_data"]

EXPOSE 80
ENTRYPOINT ["./entrypoint.sh"]
