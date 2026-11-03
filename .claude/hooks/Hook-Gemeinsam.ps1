# Gemeinsame Bausteine der Claude-Code-Hooks (#586). Wird von den Hook-Skripten per Dot-Sourcing geladen.
#
# Claude Code übergibt jedem Hook ein JSON-Objekt über stdin (cwd, tool_name, tool_input, …). Exitcode 2 blockiert
# einen PreToolUse-Aufruf bzw. meldet bei PostToolUse etwas an Claude zurück – in beiden Fällen geht der Text auf
# stderr an Claude. Exitcode 0 lässt alles durch.

[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Lies-HookEingabe {
    $text = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
}

# Befehlstext eines Bash- oder PowerShell-Aufrufs; leer bei anderen Werkzeugen.
function Befehl-Aus($eingabe) {
    if ($null -eq $eingabe -or $null -eq $eingabe.tool_input) { return '' }
    $befehl = $eingabe.tool_input.command
    if ($null -eq $befehl) { return '' }
    return [string]$befehl
}

# Pfadangabe in Anführungszeichen oder ohne Leerzeichen und Befehlstrenner („cd ../tests; git push“)
$script:PFAD = '("[^"]*"|''[^'']*''|[^\s;&|)]+)'

# Erkennt „git commit“ als eigenen Befehl (Zeilenanfang oder nach ; & | ( ), auch mit Git-Optionen davor
# („git -C <pfad>“, „git -c k=v“, „git --no-pager“), nicht aber „git commit-tree“ (Gegenprobe-Skripte, #530) oder
# „git push“ im Fließtext eines Issue-Kommentars.
function Ist-GitBefehl([string]$befehl, [string]$unterbefehl) {
    $muster = '(?m)(^|[;&|(])\s*git(\s+(-C\s+' + $script:PFAD + '|-c\s+\S+|--[\w-]+(=\S+)?))*\s+' +
        [regex]::Escape($unterbefehl) + '(?![-\w])'
    return $befehl -match $muster
}

function Ohne-Anfuehrung([string]$pfad) {
    return $pfad.Trim('"', "'")
}

# Git-Bash-Pfad (/c/Develop/…) in einen Windows-Pfad übersetzen
function Windows-Pfad([string]$pfad) {
    if ($pfad -match '^/([a-zA-Z])(/.*)?$') { return "$($Matches[1]):$($Matches[2])" }
    return $pfad
}

# Pfad aus dem Befehl, relativ zu $von aufgelöst (wie „cd“ bzw. „git -C“ es täten)
function Pfad-Unter([string]$von, [string]$pfad) {
    $pfad = Windows-Pfad (Ohne-Anfuehrung $pfad)
    if ([System.IO.Path]::IsPathRooted($pfad)) { return $pfad }
    return Join-Path $von $pfad
}

# Repository, in dem der Git-Befehl wirkt: „git -C <pfad>“, sonst das letzte „cd <pfad>“ im Befehl, sonst das
# Arbeitsverzeichnis der Session. Leer, wenn dort kein Git-Repository liegt (z. B. Commit in einem anderen Worktree
# wird dann in diesem Worktree geprüft, nicht im Hauptrepository).
function Repo-Fuer($eingabe, [string]$befehl, [string]$unterbefehl) {
    $basis = if ($eingabe.cwd) { Windows-Pfad $eingabe.cwd } elseif ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
    $ziel = $basis
    $cds = [regex]::Matches($befehl, '(?m)(^|[;&|(])\s*(cd|Set-Location|pushd)\s+' + $script:PFAD)
    if ($cds.Count -gt 0) { $ziel = Pfad-Unter $ziel $cds[$cds.Count - 1].Groups[3].Value }
    $mitC = [regex]::Match($befehl, 'git\s+(?:-c\s+\S+\s+)*-C\s+' + $script:PFAD + '[^;&|]*?\s' + [regex]::Escape($unterbefehl))
    if ($mitC.Success) { $ziel = Pfad-Unter $ziel $mitC.Groups[1].Value }
    $wurzel = git -C $ziel rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0) { return '' }
    if (-not (Ist-Projektrepo $wurzel)) { return '' }
    return $wurzel
}

# Nur das Projekt selbst und seine Git-Worktrees: Der Hook läuft vor der Freigabe des Befehls und baut im Ziel –
# ein „cd <fremder Ordner> && git commit“ darf dort kein dotnet build (MSBuild-Code) auslösen (Sicherheitsprüfung).
function Ist-Projektrepo([string]$wurzel) {
    $projekt = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Resolve-Path "$PSScriptRoot/../..").Path }
    $erlaubt = @(git -C $projekt worktree list --porcelain 2>$null |
        Where-Object { $_ -like 'worktree *' } | ForEach-Object { $_.Substring(9) })
    $erlaubt += $projekt
    $normal = { param($p) [System.IO.Path]::GetFullPath((Windows-Pfad $p)).TrimEnd('\', '/').ToLowerInvariant() }
    $kandidat = & $normal $wurzel
    return @($erlaubt | Where-Object { (& $normal $_) -eq $kandidat }).Count -gt 0
}

# Pfade aus „git status --porcelain=v1“: Umbenennung „alt -> neu“ zählt mit dem neuen Pfad, Anführungszeichen weg
function Geaenderte-Dateien {
    return @(git status --porcelain=v1 --untracked-files=all | ForEach-Object {
        Ohne-Anfuehrung ($_.Substring(3) -replace '^.* -> ', '')
    })
}

function Melde-AnClaude([string]$text) {
    [Console]::Error.WriteLine($text)
}
