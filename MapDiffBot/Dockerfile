FROM rust AS compiler
WORKDIR /src/SpacemanDMM

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
	cmake \
	&& rm -rf /var/lib/apt/lists/*

COPY SpacemanDMM/ ./

RUN cargo build -p cli --release

FROM microsoft/dotnet:2.1-sdk AS build

WORKDIR /src
COPY MapDiffBot/MapDiffBot.csproj MapDiffBot/
COPY MapDiffBot.Tests/MapDiffBot.Tests.csproj MapDiffBot.Tests/
RUN cd MapDiffBot.Tests && dotnet restore

COPY MapDiffBot/* MapDiffBot/
COPY MapDiffBot.Tests/* MapDiffBot.Tests/

WORKDIR /src/MapDiffBot.Tests
RUN dotnet test -c Docker

WORKDIR /src/MapDiffBot
RUN dotnet publish -c Docker -o /app && cp entrypoint.sh /app/

FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
EXPOSE 80

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
	&& rm -rf /var/lib/apt/lists/* \
	&& mkdir /config_data

WORKDIR /app
COPY --from=compiler /src/SpacemanDMM/target/release/dmm-tools dmm-tools.exe
COPY --from=build /app .

RUN mv appsettings.Docker.json /config_data/appsettings.Production.json
VOLUME ["/config_data", "/app/App_Data/Repositories" ]

ENTRYPOINT ["./entrypoint.sh"]
