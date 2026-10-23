<#
.SYNOPSIS
    Wertet die SARIF-Befunde der statischen Codeanalyse aus (#498).
.DESCRIPTION
    Liest analyse/*.sarif (vom letzten Build geschrieben) und zählt die Befunde je Regel. Mit -Regel werden die
    Fundstellen einzelner Regeln aufgelistet. Unterdrückte Befunde (#pragma, SuppressMessage) zählen nicht mit;
    doppelte Einträge aus mehreren Zielplattformen eines Projekts werden zusammengefasst.
.EXAMPLE
    ./build/Analyse-Auswerten.ps1
    ./build/Analyse-Auswerten.ps1 -Regel S3776,S138
    ./build/Analyse-Auswerten.ps1 -MitHinweisen
#>
param(
    [string[]] $Regel,
    # Auch Befunde der Stufe "note" (suggestion) zählen, nicht nur Warnungen
    [switch] $MitHinweisen
)

[Console]::OutputEncoding = [Text.Encoding]::UTF8

# Über "pwsh -File" kommt eine Komma-Liste als ein einziger String an
$Regel = $Regel -split ',' | Where-Object { $_ }
$wurzel = Split-Path $PSScriptRoot -Parent
$dateien = Get-ChildItem (Join-Path $wurzel 'analyse') -Filter *.sarif -ErrorAction SilentlyContinue
if (-not $dateien) { Write-Error 'Keine SARIF-Dateien unter analyse/ – erst bauen.'; exit 1 }

$befunde = foreach ($datei in $dateien) {
    $sarif = Get-Content $datei.FullName -Raw -Encoding utf8 | ConvertFrom-Json
    foreach ($lauf in $sarif.runs) {
        foreach ($r in $lauf.results) {
            if ($r.suppressions) { continue }
            $stufe = if ($r.level) { $r.level } else { 'warning' }
            if (-not $MitHinweisen -and $stufe -ne 'warning' -and $stufe -ne 'error') { continue }
            $ort = $r.locations[0].physicalLocation
            $pfad = if ($ort) { ([uri]$ort.artifactLocation.uri).LocalPath.Replace("$wurzel\", '') } else { '' }
            [pscustomobject]@{
                Regel   = $r.ruleId
                Stufe   = $stufe
                Datei   = $pfad
                Zeile   = $ort.region.startLine
                Meldung = $r.message.text
            }
        }
    }
}
$befunde = $befunde | Sort-Object Regel, Datei, Zeile, Meldung -Unique

if ($Regel) {
    $befunde | Where-Object Regel -in $Regel |
        ForEach-Object { '{0,-8} {1}:{2}  {3}' -f $_.Regel, $_.Datei, $_.Zeile, $_.Meldung }
}
else {
    $befunde | Group-Object Regel, Stufe | Sort-Object Count -Descending |
        ForEach-Object { '{0,5}  {1}' -f $_.Count, $_.Name }
    '{0,5}  gesamt' -f @($befunde).Count
}
