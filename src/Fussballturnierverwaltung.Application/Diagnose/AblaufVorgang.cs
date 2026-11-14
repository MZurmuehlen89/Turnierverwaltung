using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Fussballturnierverwaltung.Application.Diagnose;

/// <summary>Ablaufprotokoll (#413): schreibt Beginn und Ende eines Vorgangs samt Dauer ins Log und führt Buch
/// über die gerade offenen Vorgänge. Zweck ist die Aufklärung sporadischer Überlappungen (DbContext-Kollision
/// "second operation") — dort fehlte bisher, welcher Vorgang den Kontext gerade belegte und wer ihn gestartet
/// hat. Die Liste der offenen Vorgänge wird unabhängig vom Log-Schalter geführt, damit die Kollisionsdiagnose
/// sie auch bei abgeschaltetem Protokoll ausgeben kann. Seit #323 speist sie auch die zentrale Ladeanzeige
/// (<see cref="Geaendert"/>, <see cref="Offene"/>). Seit #49 nennt die Endezeile auch den Datenbankanteil des
/// Vorgangs (<see cref="DbBefehlErfassen"/>, befüllt von der Infrastructure).</summary>
public static class AblaufVorgang
{
    private sealed class Eintrag(string beschreibung, string? anzeige)
    {
        public string Beschreibung { get; } = beschreibung;
        public string? Anzeige { get; } = anzeige;
        public Stopwatch Dauer { get; } = Stopwatch.StartNew();
        public DbAnteil Db { get; } = new();
    }

    /// <summary>Die offenen Vorgänge des aktuellen asynchronen Ablaufs, innerster zuerst.</summary>
    private sealed record Kette(Eintrag Eintrag, Kette? Aussen);

    private sealed class Abschluss(ILogger logger, long id, Eintrag eintrag, Kette kette) : IDisposable
    {
        private bool _beendet;

        public void Dispose()
        {
            if (_beendet) return;
            _beendet = true;
            _offen.TryRemove(id, out _);
            // Nur zurücksetzen, wenn die Kette noch die eigene ist: ein Dispose aus einem anderen Ablauf darf dort
            // nichts verbiegen.
            if (_kette.Value == kette) _kette.Value = kette.Aussen;

            if (eintrag.Db.Beschreiben() is { } db)
                logger.LogInformation("Ende:   {Vorgang} ({Dauer} ms; {DbAnteil})",
                    eintrag.Beschreibung, eintrag.Dauer.ElapsedMilliseconds, db);
            else
                logger.LogInformation("Ende:   {Vorgang} ({Dauer} ms)", eintrag.Beschreibung, eintrag.Dauer.ElapsedMilliseconds);
            Geaendert?.Invoke();
        }
    }

    private static readonly ConcurrentDictionary<long, Eintrag> _offen = new();
    private static long _naechsteId;

    // DB-Anteil je Vorgang (#49): Die Kette fließt mit dem ExecutionContext durch die awaits, so landet ein Befehl
    // bei genau den Vorgängen, in deren Ablauf er läuft — auch wenn parallel ein anderer Vorgang offen ist.
    // Vorgang() ist synchron, deshalb bleibt der gesetzte Wert beim Aufrufer sichtbar (bei einer async-Methode
    // würde er beim Verlassen zurückgesetzt).
    private static readonly AsyncLocal<Kette?> _kette = new();

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
        var kette = new Kette(eintrag, _kette.Value);
        _kette.Value = kette;

        if (laeuftBereits.Length == 0)
            logger.LogInformation("Beginn: {Vorgang}", beschreibung);
        else
            logger.LogInformation("Beginn: {Vorgang} — läuft bereits: {Offen}", beschreibung, laeuftBereits);

        Geaendert?.Invoke();
        return new Abschluss(logger, id, eintrag, kette);
    }

    // ── DB-Anteil (#49) ─────────────────────────────────────────────────────

    /// <summary>Rechnet einen Datenbankbefehl allen offenen Vorgängen des aktuellen Ablaufs zu — verschachtelte
    /// Vorgänge enthalten so auch die Befehle ihrer inneren. Außerhalb eines Vorgangs ohne Wirkung.</summary>
    /// <param name="ausfuehrung">Ausführung des Befehls bis zum ersten Ergebnis (Untergrenze).</param>
    /// <param name="bisReaderGeschlossen">bis der Reader geschlossen ist, mit dem Einlesen der Zeilen (Obergrenze);
    /// bei Befehlen ohne Reader gleich <paramref name="ausfuehrung"/>.</param>
    public static void DbBefehlErfassen(TimeSpan ausfuehrung, TimeSpan bisReaderGeschlossen) =>
        AllenOffenenZurechnen(db => db.BefehlErfassen(ausfuehrung, bisReaderGeschlossen));

    /// <summary>Rechnet den Aufbau einer Datenbankverbindung allen offenen Vorgängen des aktuellen Ablaufs zu.</summary>
    public static void DbVerbindungErfassen(TimeSpan dauer) => AllenOffenenZurechnen(db => db.VerbindungErfassen(dauer));

    /// <summary>Rechnet ein SaveChanges allen offenen Vorgängen des aktuellen Ablaufs zu.</summary>
    public static void DbSpeichernErfassen(TimeSpan dauer) => AllenOffenenZurechnen(db => db.SpeichernErfassen(dauer));

    private static void AllenOffenenZurechnen(Action<DbAnteil> erfassen)
    {
        for (var k = _kette.Value; k is not null; k = k.Aussen)
            erfassen(k.Eintrag.Db);
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
