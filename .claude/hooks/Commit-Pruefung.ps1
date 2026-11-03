# PreToolUse-Hook (#586): vor „git commit“ bauen, Unit-Tests ausführen und auf fehlende EF-Migrationen prüfen –
# jeweils nur, wenn die Änderung den Bereich betrifft. Rot → Commit blockiert, Befund geht an Claude.
#
# Geprüft wird der Arbeitsstand (gestaged, ungestaged und neue Dateien), nicht nur der Index: Befehle wie
# „git add -A && git commit …“ stagen erst, nachdem dieser Hook gelaufen ist.

. "$PSScriptRoot/Hook-Gemeinsam.ps1"

$eingabe = Lies-HookEingabe
$befehl = Befehl-Aus $eingabe
if (-not (Ist-GitBefehl $befehl 'commit')) { exit 0 }

Set-Location $script:Repo

# ── Betroffene Bereiche ────────────────────────
$dateien = @(git status --porcelain=v1 --untracked-files=all | ForEach-Object { $_.Substring(3).Trim('"') -replace '^.* -> ', '' })
$code = @($dateien | Where-Object { $_ -match '^(src|tests)/' -and $_ -match '\.(cs|xaml|csproj|props|targets|json|resx)$' }) +
        @($dateien | Where-Object { $_ -match '^(Directory\.Build\.props|\.globalconfig|global\.json|build/SonarLint\.xml|.*\.slnx)$' })
$ohneWinUi = @($code | Where-Object { $_ -notmatch '^src/Fussballturnierverwaltung\.WinUi/' -and $_ -notmatch '^tests/(Fussballturnierverwaltung\.GuiTests|GuiTest\.)' })
$modell = @($dateien | Where-Object { $_ -match '^src/Fussballturnierverwaltung\.Domain/' -or $_ -match '^src/Fussballturnierverwaltung\.Infrastructure/Persistenz/(Konfigurationen/|TurnierverwaltungDbContext\.cs)' })

if ($code.Count -eq 0) { exit 0 }

$befunde = [System.Collections.Generic.List[string]]::new()

# ── Build ────────────────────────
$build = dotnet build Fussballturnierverwaltung.slnx --no-restore -v q -nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    $fehler = @($build | Where-Object { $_ -match ': error ' } | Select-Object -Unique -First 15)
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
        $befunde.Add("Modelländerung ohne Migration (Domain/EF-Konfiguration geändert):`n" + (($ef | Select-Object -Last 3) -join "`n") +
            "`n→ dotnet ef migrations add <Name> … und danach sofort dotnet ef database update (siehe Infrastructure/CLAUDE.md).")
    }
}

# ── Unit-Tests ────────────────────────
# Schwankt unter Last (#549) und ist direkt nach dem Build oft rot: wird einmal einzeln wiederholt, bevor er zählt.
$wackelig = @('Fussballturnierverwaltung.Tests.Auslosungen.ManuelleZiehungTests.Pokal_64TeamsMitLand_GehtImmerAuf')

function Rote-Tests($ausgabe) {
    return @($ausgabe | ForEach-Object { if ($_ -match '^(fehlerhaft|failed)\s+(\S+)\s+\(') { $Matches[2] } })
}

if ($befunde.Count -eq 0 -and $ohneWinUi.Count -gt 0) {
    $tests = dotnet test --project tests/Fussballturnierverwaltung.Tests --no-build 2>&1
    if ($LASTEXITCODE -ne 0) {
        $rot = Rote-Tests $tests
        $nurWackelig = $rot.Count -gt 0 -and @($rot | Where-Object { $_ -notin $wackelig }).Count -eq 0
        if ($nurWackelig) {
            $methode = ($rot | ForEach-Object { $_.Split('.')[-1] }) -join ' '
            $tests = dotnet test --project tests/Fussballturnierverwaltung.Tests --no-build -- --filter-method "*$methode" 2>&1
        }
        if ($LASTEXITCODE -ne 0) {
            $details = @($tests | Select-String -Pattern '^(fehlerhaft|failed)\s' -Context 0, 4 | Select-Object -First 10 |
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
