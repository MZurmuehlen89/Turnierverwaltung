using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Fussballturnierverwaltung.Application.Diagnose;

/// <summary>Ablaufprotokoll (#413): schreibt Beginn und Ende eines Vorgangs samt Dauer ins Log und führt Buch
/// über die gerade offenen Vorgänge. Zweck ist die Aufklärung sporadischer Überlappungen (DbContext-Kollision
/// "second operation") — dort fehlte bisher, welcher Vorgang den Kontext gerade belegte und wer ihn gestartet
/// hat. Die Liste der offenen Vorgänge wird unabhängig vom Log-Schalter geführt, damit die Kollisionsdiagnose
/// sie auch bei abgeschaltetem Protokoll ausgeben kann.</summary>
public static class AblaufVorgang
{
    private sealed class Eintrag(string beschreibung)
    {
        public string Beschreibung { get; } = beschreibung;
        public Stopwatch Dauer { get; } = Stopwatch.StartNew();
    }

    private sealed class Abschluss(ILogger logger, long id, Eintrag eintrag) : IDisposable
    {
        private bool _beendet;

        public void Dispose()
        {
            if (_beendet) return;
            _beendet = true;
            _offen.TryRemove(id, out _);
            logger.LogInformation("Ende:   {Vorgang} ({Dauer} ms)", eintrag.Beschreibung, eintrag.Dauer.ElapsedMilliseconds);
        }
    }

    private static readonly ConcurrentDictionary<long, Eintrag> _offen = new();
    private static long _naechsteId;

    /// <summary>Beginnt einen Vorgang; das Ende wird beim Dispose protokolliert (<c>using</c>), auch wenn der
    /// Vorgang mit einer Ausnahme abbricht.</summary>
    public static IDisposable Vorgang(this ILogger logger, string beschreibung)
    {
        // Was beim Beginn schon läuft, steht direkt in der Zeile — eine Überlappung ist so ohne Suchen sichtbar.
        string laeuftBereits = OffeneBeschreiben();
        var eintrag = new Eintrag(beschreibung);
        long id = Interlocked.Increment(ref _naechsteId);
        _offen[id] = eintrag;

        if (laeuftBereits.Length == 0)
            logger.LogInformation("Beginn: {Vorgang}", beschreibung);
        else
            logger.LogInformation("Beginn: {Vorgang} — läuft bereits: {Offen}", beschreibung, laeuftBereits);

        return new Abschluss(logger, id, eintrag);
    }

    /// <summary>Die gerade offenen Vorgänge in Startreihenfolge, je mit bisheriger Dauer; leer, wenn keiner läuft.</summary>
    public static string OffeneBeschreiben() =>
        string.Join("; ", _offen
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Value.Beschreibung} (seit {kv.Value.Dauer.ElapsedMilliseconds} ms)"));
}
