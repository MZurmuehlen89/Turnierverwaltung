<#
.SYNOPSIS
    Führt Mutation Testing mit Stryker.NET auf der Fachlogik aus (#541).

.DESCRIPTION
    Stryker baut gezielt kleine Fehler in den Code ein (Mutanten) und prüft, ob mindestens ein Unit-Test rot wird.
    Welche Dateien mutiert werden, steht in tests/Fussballturnierverwaltung.Tests/stryker-config.json (Auslosungen,
    Spielplan, Tabelle, Simulation in Application, ohne die *Service-Klassen). Der Bericht landet unter
    artifacts/stryker/<Zeitstempel>/reports/mutation-report.html.

    Ein voller Lauf dauert Stunden und lastet die CPU stark aus. Für die tägliche Arbeit -Seit nutzen: dann prüft
    Stryker nur Mutanten in Dateien, die sich seit dem angegebenen Commit geändert haben.

.PARAMETER Seit
    Nur seit diesem Commit/Branch geänderte Dateien testen (Stryker --since), z. B. "main" oder "HEAD~3".

.PARAMETER Mutieren
    Eigene Dateiauswahl statt der aus stryker-config.json, z. B. "**/Tabelle/TabellenRechner.cs".

.PARAMETER Nebenlaeufigkeit
    Anzahl paralleler Testläufe (Stryker --concurrency). Standard ist die Hälfte der Kerne.

.PARAMETER Oeffnen
    Bericht nach dem Lauf im Browser öffnen.

.EXAMPLE
    pwsh build/Mutation-Ausfuehren.ps1 -Seit main
.EXAMPLE
    pwsh build/Mutation-Ausfuehren.ps1 -Mutieren "**/Tabelle/TabellenRechner.cs" -Oeffnen
#>
param(
    [string]$Seit,
    [string]$Mutieren,
    [int]$Nebenlaeufigkeit,
    [switch]$Oeffnen
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$testprojekt = Join-Path $repo 'tests\Fussballturnierverwaltung.Tests'
$ausgabe = Join-Path $repo "artifacts\stryker\$(Get-Date -Format 'yyyyMMdd_HHmmss')"

$argumente = @('--output', $ausgabe)
if ($Seit) { $argumente += "--since:$Seit" }
if ($Mutieren) { $argumente += @('--mutate', $Mutieren) }
if ($Nebenlaeufigkeit -gt 0) { $argumente += @('--concurrency', $Nebenlaeufigkeit) }
if ($Oeffnen) { $argumente += '--open-report' }

dotnet tool restore | Out-Null
# Lässt Tests aussetzen, die unter Strykers Instrumentierung zu langsam sind (Mutationslauf.cs im Testprojekt)
$env:TURNIERVERWALTUNG_MUTATIONSLAUF = '1'
Push-Location $testprojekt
try {
    # Stryker liest stryker-config.json aus dem aktuellen Verzeichnis
    dotnet stryker @argumente
    $exitcode = $LASTEXITCODE
}
finally {
    Pop-Location
}

Write-Host "Bericht: $(Join-Path $ausgabe 'reports\mutation-report.html')"
exit $exitcode
