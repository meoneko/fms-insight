Param( [string]$version)
Write-Host "Building FMS Insight reverse proxy version $version"

Push-Location
Set-Location (Split-Path -parent $PSCommandPath)

if (Test-Path reverse-proxy.zip) {
  Remove-Item reverse-proxy.zip
}

dotnet publish -r win10-x64 --self-contained -c Release /p:Version=$version /p:PublishSingleFile=true

Get-ChildItem -Recurse bin/Release/netcoreapp3.1

& 7z a reverse-proxy.zip * -xr!bin -xr!obj -xr!build-output
& 7z a reverse-proxy.zip ./bin/Release/netcoreapp3.1/win10-x64/publish/reverse-proxy.exe

Pop-Location