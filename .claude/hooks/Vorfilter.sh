#!/usr/bin/env bash
# Vorfilter der Claude-Code-Hooks (#586): Ein pwsh-Start kostet rund 0,7 s und die Hooks hängen an jedem Bash-/
# PowerShell-Aufruf. Die PowerShell-Skripte laufen deshalb nur, wenn die Hook-Eingabe das Muster enthält.
#
# Aufruf: Vorfilter.sh <ERE-Muster> <Skript.ps1> [<Skript.ps1> …] – die Skripte laufen nacheinander mit derselben
# Eingabe; der erste Exitcode ungleich 0 (2 = blockieren) wird weitergegeben.
#
# Nur bash-eigene Mittel (kein cat/grep/dirname, kein /dev/stdin): Läuft bash ohne die Umgebung von Git Bash, fehlen
# diese, und der Filter hätte stillschweigend alles durchgelassen. „read“ endet am Eingabeende mit Status 1, daher || true.

IFS= read -r -d '' eingabe || true
muster=$1
[[ $eingabe =~ $muster ]] || exit 0
shift
for skript in "$@"; do
    printf '%s' "$eingabe" | pwsh -NoProfile -NonInteractive -File "${0%/*}/$skript" || exit $?
done
exit 0
