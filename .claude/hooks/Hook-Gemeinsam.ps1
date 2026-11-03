# Gemeinsame Bausteine der Claude-Code-Hooks (#586). Wird von den Hook-Skripten per Dot-Sourcing geladen.
#
# Claude Code übergibt jedem Hook ein JSON-Objekt über stdin (tool_name, tool_input, …). Exitcode 2 blockiert
# einen PreToolUse-Aufruf bzw. meldet bei PostToolUse etwas an Claude zurück – in beiden Fällen geht der Text auf
# stderr an Claude. Exitcode 0 lässt alles durch.

[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$script:Repo = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Resolve-Path "$PSScriptRoot/../..").Path }

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

# Erkennt „git commit“, „git -C <pfad> commit“ usw. als eigenen Befehl (Zeilenanfang oder nach ; & | ( ), nicht aber
# „git commit-tree“ (Gegenprobe-Skripte, #530) oder „git push“ im Fließtext eines Issue-Kommentars.
function Ist-GitBefehl([string]$befehl, [string]$unterbefehl) {
    $muster = '(?m)(^|[;&|(])\s*git(\s+-C\s+("[^"]*"|''[^'']*''|\S+))?\s+' + [regex]::Escape($unterbefehl) + '(?![-\w])'
    return $befehl -match $muster
}

function Melde-AnClaude([string]$text) {
    [Console]::Error.WriteLine($text)
}
