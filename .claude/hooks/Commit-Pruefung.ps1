# PreToolUse-Hook (#586): vor „git commit“ bauen, Unit-Tests ausführen und auf fehlende EF-Migrationen prüfen –
# jeweils nur, wenn die Änderung den Bereich betrifft. Rot → Commit blockiert, Befund geht an Claude.
#
# Geprüft wird der Arbeitsstand (gestaged, ungestaged und neue Dateien), nicht nur der Index: Befehle wie
# „git add -A && git commit …“ stagen erst, nachdem dieser Hook gelaufen ist.

. "$PSScriptRoot/Hook-Gemeinsam.ps1"

$eingabe = Lies-HookEingabe
$befehl = Befehl-Aus $eingabe
if (-not (Ist-GitBefehl $befehl 'commit')) { exit 0 }

$repo = Repo-Fuer $eingabe $befehl 'commit'
if (-not $repo -or -not (Test-Path (Join-Path $repo 'Fussballturnierverwaltung.slnx'))) { exit 0 }
Set-Location $repo

# ── Betroffene Bereiche ────────────────────────
$dateien = Geaenderte-Dateien
$buildKonfig = @($dateien | Where-Object { $_ -match '(\.(csproj|props|targets|slnx)|^global\.json|^\.globalconfig|^build/SonarLint\.xml)$' })
$code = @($dateien | Where-Object { $_ -match '^(src|tests)/' -and $_ -match '\.(cs|xaml|json|resx)$' }) + $buildKonfig
$ohneWinUi = @($code | Where-Object { $_ -notmatch '^src/Fussballturnierverwaltung\.WinUi/' -and $_ -notmatch '^tests/(Fussballturnierverwaltung\.GuiTests|GuiTest\.)' })
$modell = @($dateien | Where-Object { $_ -match '^src/Fussballturnierverwaltung\.Domain/' -or
    $_ -match '^src/Fussballturnierverwaltung\.Infrastructure/Persistenz/(Konfigurationen/|Migrationen/|TurnierverwaltungDbContext\.cs)' })

if ($code.Count -eq 0) { exit 0 }

$befunde = [System.Collections.Generic.List[string]]::new()

# ── Build ────────────────────────
# Restore nur bei geänderten Projekt-/Build-Dateien (gemessen: 64 s statt 26 s), sonst baut ein geänderter
# Paketverweis gegen die alte project.assets.json
$argumente = [System.Collections.Generic.List[string]]@('build', 'Fussballturnierverwaltung.slnx', '-v', 'q', '-nologo')
if ($buildKonfig.Count -eq 0) { $argumente.Add('--no-restore') }
$build = dotnet @argumente 2>&1
if ($LASTEXITCODE -ne 0) {
    $fehler = @($build | Where-Object { $_ -match ': error ' } | Select-Object -Unique -First 15)
    if ($fehler.Count -eq 0) { $fehler = @($build | Select-Object -Last 15) }
    $befunde.Add("Build fehlgeschlagen:`n" + ($fehler -join "`n"))
}

# ── Migrationen ────────────────────────
# Startprojekt Infrastructure (Design-Time-Factory), nicht WinUi: der Solution-Build schreibt WinUi nach bin\x64\Debug,
# „dotnet ef --no-build“ liest dort aber bin\Debug – und prüfte so gegen ein veraltetes Modell.
if ($befunde.Count -eq 0 -and $modell.Count -gt 0) {
    $ef = dotnet ef migrations has-pending-model-changes `
        --project src/Fussballturnierverwaltung.Infrastructure `
        --startup-project src/Fussballturnierverwaltung.Infrastructure --no-build 2>&1
    if ($LASTEXITCODE -ne 0) {
        $letzte = ($ef | Select-Object -Last 5) -join "`n"
        if ($letzte -match 'Changes have been made to the model') {
            $befunde.Add("Modelländerung ohne Migration (Domain/EF-Konfiguration geändert):`n$letzte" +
                "`n→ dotnet ef migrations add <Name> … und danach sofort dotnet ef database update (siehe Infrastructure/CLAUDE.md).")
        } else {
            $befunde.Add("Migrationsprüfung (dotnet ef) fehlgeschlagen – kein Modellbefund, sondern ein Werkzeugfehler:`n$letzte")
        }
    }
}

# ── Unit-Tests ────────────────────────
# Schwankt unter Last (#549) und ist direkt nach dem Build oft rot: wird einmal einzeln wiederholt, bevor er zählt.
$wackelig = @('Fussballturnierverwaltung.Tests.Auslosungen.ManuelleZiehungTests.Pokal_64TeamsMitLand_GehtImmerAuf')
$ROT = '^(fehlerhaft|failed)\s+(\S+)\s+\('

if ($befunde.Count -eq 0 -and $ohneWinUi.Count -gt 0) {
    $tests = dotnet test --project tests/Fussballturnierverwaltung.Tests --no-build 2>&1
    if ($LASTEXITCODE -ne 0) {
        $rot = @($tests | ForEach-Object { if ($_ -match $ROT) { $Matches[2] } })
        $nurWackelig = $rot.Count -gt 0 -and @($rot | Where-Object { $_ -notin $wackelig }).Count -eq 0
        if ($nurWackelig) {
            $methode = ($rot | ForEach-Object { $_.Split('.')[-1] }) -join ' '
            $tests = dotnet test --project tests/Fussballturnierverwaltung.Tests --no-build -- --filter-method "*$methode" 2>&1
        }
        if ($LASTEXITCODE -ne 0) {
            $details = @($tests | Select-String -Pattern $ROT -Context 0, 4 | Select-Object -First 10 |
                ForEach-Object { $_.Line; $_.Context.PostContext })
            $befunde.Add("Unit-Tests rot:`n" + ($details -join "`n") + "`n" + (($tests | Select-Object -Last 6) -join "`n"))
        }
    }
}

if ($befunde.Count -gt 0) {
    Melde-AnClaude ("Commit blockiert durch die Commit-Prüfung (.claude/hooks/Commit-Pruefung.ps1, #586).`n`n" + ($befunde -join "`n`n"))
    exit 2
}
exit 0
