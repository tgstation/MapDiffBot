#!/bin/sh
cp /config_data/appsettings.Production.json .
exec dotnet MapDiffBot.dll
