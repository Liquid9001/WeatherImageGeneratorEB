#!/usr/bin/env pwsh
# Requires: PowerShell 7+, Azure CLI, .NET 8 SDK
# Usage:
#   az login
#   az account set --subscription "<YOUR SUB ID OR NAME>"
#   ./deploy.ps1

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ------------------ CONFIG ------------------
$ResourceGroup   = "AzureSSP1EB"
$Location        = "West Europe"

# MUST be globally unique:
$StorageName     = "stweatherimg2026$(Get-Random -Maximum 9999)"
$FunctionAppName = "func-weatherimg-2026-$(Get-Random -Maximum 9999)"
$AppInsightsName = "$FunctionAppName-ai"

$BicepFile       = "main.bicep"
$CsprojPath      = (Resolve-Path "WeatherImageGenerator.csproj").Path

# Optional: set to true to make blobs publicly readable (less secure).
$PublicBlobAccess = $false
# ------------------------------------------------

Write-Host ">>> Checking Azure CLI context..."
$acct = az account show --only-show-errors | ConvertFrom-Json
if (-not $acct) { throw "Not logged in. Run 'az login' first." }
Write-Host "    Subscription:" $acct.name "("$acct.id")"

Write-Host ">>> Ensuring resource group '$ResourceGroup' in '$Location' ..."
az group create -g $ResourceGroup -l $Location --only-show-errors | Out-Null

Write-Host ">>> Deploying infrastructure via $BicepFile ..."
$deploy = az deployment group create `
  -g $ResourceGroup `
  -f $BicepFile `
  -p location=$Location `
     storageAccountName=$StorageName `
     functionAppName=$FunctionAppName `
     appInsightsName=$AppInsightsName `
     publicBlobAccess=$PublicBlobAccess `
  --only-show-errors | ConvertFrom-Json

if (-not $deploy) { throw "Bicep deployment failed." }

$FunctionAppName = $deploy.properties.outputs.functionAppName.value
$faHost = $deploy.properties.outputs.functionDefaultHostname.value
Write-Host "    Function App deployed: $FunctionAppName"
Write-Host "    Hostname:" $faHost

Write-Host ">>> Publishing Functions project ($CsprojPath) ..."
dotnet publish $CsprojPath -c Release

$projectDir = Split-Path -Path $CsprojPath -Parent
$publishDir = Join-Path $projectDir "bin\Release\net8.0\publish"
$zipPath = Join-Path $projectDir "app.zip"

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host ">>> Zipping package: $zipPath"
Push-Location $publishDir
Compress-Archive -Path * -DestinationPath $zipPath -Force
Pop-Location

$absoluteZipPath = (Resolve-Path $zipPath).Path

Write-Host ">>> Deploying package to Azure Function App '$FunctionAppName' ..."
az functionapp deployment source config-zip `
  -g $ResourceGroup `
  -n $FunctionAppName `
  --src "$absoluteZipPath" `
  --only-show-errors | Out-Null


Write-Host ">>> Configuring Environment Variables in Azure..."

$localSettingsPath = Join-Path $projectDir "local.settings.json"
if (Test-Path $localSettingsPath) {
    $settings = Get-Content $localSettingsPath | ConvertFrom-Json
    $pexelsKey = $settings.Values.PEXELS_API_KEY

    if ($pexelsKey) {
        Write-Host "    Found PEXELS_API_KEY. Pushing to Azure..."
        az functionapp config appsettings set `
            -g $ResourceGroup `
            -n $FunctionAppName `
            --settings PEXELS_API_KEY="$pexelsKey" `
            --only-show-errors | Out-Null
        Write-Host "    Successfully configured PEXELS_API_KEY in the cloud!"
    } else {
        Write-Warning "    PEXELS_API_KEY not found in local.settings.json."
    }
}

Write-Host ""
Write-Host "Deploy complete."
Write-Host ("Base URL: https://{0}/api" -f $faHost)
Write-Host ("Start job: https://{0}/api/jobs/start" -f $faHost)
Write-Host ("Status:   https://{0}/api/jobs/<jobId>/status" -f $faHost)
Write-Host ("Images:   https://{0}/api/jobs/<jobId>/images" -f $faHost)