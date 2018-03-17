FROM microsoft/aspnetcore-build:2.0.6-2.1.101-jessie AS base
WORKDIR /app
EXPOSE 80

FROM rust AS compiler
WORKDIR /src
COPY . .
WORKDIR /src/SpacemanDMM
RUN cargo build -p cli --release

FROM microsoft/aspnetcore-build:2.0.6-2.1.101-jessie AS build
WORKDIR /src/SpacemanDMM/target/release/
COPY --from=compiler /src/SpacemanDMM/target/release/dmm-tools .
WORKDIR /src
COPY . .
RUN dotnet restore MapDiffBot/MapDiffBot.csproj
WORKDIR /src/MapDiffBot
RUN dotnet build -c Docker -o /app

FROM build AS publish
RUN dotnet publish -c Docker -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "MapDiffBot.dll"]
