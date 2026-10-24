<#
.SYNOPSIS
    Baut die ungepackte Testvariante der App und führt die GUI-Tests aus (#494, #521).

.DESCRIPTION
    Die GUI-Tests steuern die echte App per UI Automation. Dafür wird die App ungepackt und eigenständig
    (WindowsPackageType=None, WindowsAppSDKSelfContained=true) nach artifacts/guitest gebaut – getrennt vom
    normalen MSIX-/F5-Build, der unberührt bleibt. Jeder Lauf bekommt eine eigene Datenbank auf dem lokalen
    SQL Server, die am Ende wieder gelöscht wird; Datenpfad, Logs und Belege fehlgeschlagener Tests liegen
    unter artifacts/guitest/laeufe/<Lauf>.

    Während des Laufs Maus und Tastatur nicht benutzen: Die Tests klicken im Fenster der App.

.PARAMETER OhneBau
    Die Testvariante nicht neu bauen (spart rund eine Minute, wenn sich an der App nichts geändert hat).

.PARAMETER XunitArgumente
    Weitere Argumente für den xunit-Runner, z. B. "-class Fussballturnierverwaltung.GuiTests.NavigationSmokeTests".

.EXAMPLE
    pwsh build/GuiTests-Ausfuehren.ps1
.EXAMPLE
    pwsh build/GuiTests-Ausfuehren.ps1 -OhneBau -method "*Bereich_OeffnetSichOhneFehler"
#>
param(
    [switch]$OhneBau,
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$XunitArgumente = @()
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$artefakte = Join-Path $repo 'artifacts\guitest'

if (-not $OhneBau) {
    Write-Host 'Baue die Testvariante der App (ungepackt, eigenständig) ...'
    dotnet build (Join-Path $repo 'src\Fussballturnierverwaltung.WinUi') -p:Platform=x64 `
        -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true --artifacts-path $artefakte -v q -nologo
    if ($LASTEXITCODE -ne 0) { throw "Bau der Testvariante fehlgeschlagen (Exitcode $LASTEXITCODE)." }
}

Write-Host 'Starte die GUI-Tests – Maus und Tastatur bitte nicht benutzen ...'
dotnet run --project (Join-Path $repo 'tests\Fussballturnierverwaltung.GuiTests') -- @XunitArgumente
exit $LASTEXITCODE
