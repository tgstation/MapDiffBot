$bf = $Env:APPVEYOR_BUILD_FOLDER

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo("$bf/MapDiffBot.Tests/bin/Release/MapDiffBot.Tests.dll").FileVersion
