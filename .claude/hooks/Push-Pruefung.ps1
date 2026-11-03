# PreToolUse-Hook (#586): kein „git push“, solange uncommittete Änderungen im Arbeitsverzeichnis liegen
# („Push erst ohne offene Arbeit“).

. "$PSScriptRoot/Hook-Gemeinsam.ps1"

$eingabe = Lies-HookEingabe
$befehl = Befehl-Aus $eingabe
if (-not (Ist-GitBefehl $befehl 'push')) { exit 0 }

$repo = Repo-Fuer $eingabe $befehl 'push'
if (-not $repo) { exit 0 }
Set-Location $repo
$offen = @(git status --porcelain=v1 --untracked-files=normal)
if ($offen.Count -gt 0) {
    Melde-AnClaude ("Push blockiert (.claude/hooks/Push-Pruefung.ps1, #586): Es gibt uncommittete Änderungen in $repo – erst committen oder bewusst verwerfen, dann pushen.`n" +
        (($offen | Select-Object -First 15) -join "`n"))
    exit 2
}
exit 0
