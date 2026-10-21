#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateRange(1, [int]::MaxValue)]
    [int]$BcUserId = 1017,
    [Parameter(Mandatory)]
    [string]$KeysPath
)

$ErrorActionPreference = 'Stop'
if (-not $env:ConnectionStrings__DefaultConnection) {
    throw 'Set ConnectionStrings__DefaultConnection for the target database in this PowerShell session.'
}
$resolvedKeys = (Resolve-Path -LiteralPath $KeysPath).Path
if (-not (Get-ChildItem -LiteralPath $resolvedKeys -Filter 'key-*.xml' -File)) {
    throw 'This directory does not contain the original Data Protection key files.'
}
$readerProject = Join-Path $PSScriptRoot 'EntraIdentityReader/EntraIdentityReader.csproj'
& dotnet run --project $readerProject --no-launch-profile -p:UseAppHost=false -- $BcUserId $resolvedKeys
if ($LASTEXITCODE -ne 0) { throw 'Entra Object ID recovery did not complete.' }
