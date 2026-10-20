using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Fussballturnierverwaltung.Application.Diagnose;

/// <summary>Ablaufprotokoll (#413): schreibt Beginn und Ende eines Vorgangs samt Dauer ins Log und führt Buch
/// über die gerade offenen Vorgänge. Zweck ist die Aufklärung sporadischer Überlappungen (DbContext-Kollision
/// "second operation") — dort fehlte bisher, welcher Vorgang den Kontext gerade belegte und wer ihn gestartet
/// hat. Die Liste der offenen Vorgänge wird unabhängig vom Log-Schalter geführt, damit die Kollisionsdiagnose
/// sie auch bei abgeschaltetem Protokoll ausgeben kann. Seit #323 speist sie auch die zentrale Ladeanzeige
/// (<see cref="Geaendert"/>, <see cref="Offene"/>).</summary>
public static class AblaufVorgang
{
    private sealed class Eintrag(string beschreibung, string? anzeige)
    {
        public string Beschreibung { get; } = beschreibung;
        public string? Anzeige { get; } = anzeige;
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
            Geaendert?.Invoke();
        }
    }

    private static readonly ConcurrentDictionary<long, Eintrag> _offen = new();
    private static long _naechsteId;

    /// <summary>Ein Vorgang hat begonnen oder geendet. Kann auf jedem Thread feuern.</summary>
    public static event Action? Geaendert;

    /// <summary>Beginnt einen Vorgang; das Ende wird beim Dispose protokolliert (<c>using</c>), auch wenn der
    /// Vorgang mit einer Ausnahme abbricht.</summary>
    /// <param name="anzeige">Text für die Ladeanzeige, wenn der Vorgang länger dauert (z.B. "Runde wird
    /// simuliert …"); nur für Vorgänge, die der Nutzer auslöst und die nicht in einem anderen laufen.</param>
    public static IDisposable Vorgang(this ILogger logger, string beschreibung, string? anzeige = null)
    {
        // Was beim Beginn schon läuft, steht direkt in der Zeile — eine Überlappung ist so ohne Suchen sichtbar.
        string laeuftBereits = OffeneBeschreiben();
        var eintrag = new Eintrag(beschreibung, anzeige);
        long id = Interlocked.Increment(ref _naechsteId);
        _offen[id] = eintrag;

        if (laeuftBereits.Length == 0)
            logger.LogInformation("Beginn: {Vorgang}", beschreibung);
        else
            logger.LogInformation("Beginn: {Vorgang} — läuft bereits: {Offen}", beschreibung, laeuftBereits);

        Geaendert?.Invoke();
        return new Abschluss(logger, id, eintrag);
    }

    /// <summary>Die gerade offenen Vorgänge in Startreihenfolge, je mit bisheriger Dauer; leer, wenn keiner läuft.</summary>
    // ToArray ist der atomare Schnappschuss des ConcurrentDictionary. Direkt sortiert, kopierte LINQ über
    // ICollection.CopyTo (Anzahl lesen, dann kopieren) und brach bei gleichzeitigem Einfügen ab.
    public static string OffeneBeschreiben() =>
        string.Join("; ", _offen
            .ToArray()
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Value.Beschreibung} (seit {kv.Value.Dauer.ElapsedMilliseconds} ms)"));

    /// <summary>Die gerade offenen Vorgänge in Startreihenfolge, für die Ladeanzeige.</summary>
    public static IReadOnlyList<OffenerVorgang> Offene() =>
        [.. _offen
            .ToArray()
            .OrderBy(kv => kv.Key)
            .Select(kv => new OffenerVorgang(kv.Value.Dauer.Elapsed, kv.Value.Anzeige))];
}

/// <summary>Schnappschuss eines offenen Vorgangs: bisherige Dauer und optionaler Anzeigetext.</summary>
public sealed record OffenerVorgang(TimeSpan Dauer, string? Anzeige);
