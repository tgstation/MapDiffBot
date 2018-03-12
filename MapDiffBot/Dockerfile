FROM microsoft/aspnetcore AS base
WORKDIR /app
EXPOSE 80

FROM rust AS compiler
WORKDIR /src
COPY . .
WORKDIR /src/SpacemanDMM
RUN cargo build -p cli

FROM microsoft/aspnetcore-build AS build
WORKDIR /src/SpacemanDMM/target/debug/
COPY --from=compiler /src/SpacemanDMM/target/debug/dmm-tools .
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
