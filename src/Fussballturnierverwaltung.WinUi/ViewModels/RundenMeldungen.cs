using Fussballturnierverwaltung.Application.Abstraktionen;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Meldungen der Runden-Prüfung (#601): verteilt sie auf Runden, Tabellenbereiche und Übergangsregel-Zeilen und
/// merkt sich zu jeder Meldung, welche Runde (und Regel) ein Klick in der Meldungsleiste zeigen muss.
/// Prüfung der Runden: Zeile = Runde, Unterzeile = Tabellenbereich. Prüfung der Übergangsregeln: Zeile = Quellrunde bzw.
/// geprüfte Runde, Unterzeile = Position in <c>regelZeilen</c> (dieselbe Reihenfolge wie die geprüften Eingaben).</summary>
public sealed class RundenMeldungen
{
    private readonly Dictionary<Feldfehler, (RundeZeile Runde, UebergangsregelBlock? Regel)> _ziele =
        new(ReferenceEqualityComparer.Instance);

    public static RundenMeldungen Keine { get; } = new();

    /// <summary>Alle Meldungen für die Meldungsleiste – dieselben Instanzen wie an den Feldern (der Sprung sucht per Referenz).</summary>
    public IReadOnlyList<Feldfehler> Alle { get; } = [];

    private RundenMeldungen() { }

    private RundenMeldungen(IReadOnlyList<RundeZeile> runden, IReadOnlyList<Feldfehler> rundenMeldungen,
        IReadOnlyList<(UebergangsregelBlock Block, UebergangsregelZeile Zeile)> regelZeilen,
        IReadOnlyList<Feldfehler> regelMeldungen)
    {
        Alle = [.. rundenMeldungen, .. regelMeldungen];
        foreach (var meldung in rundenMeldungen)
            ZielMerken(meldung, runden, regel: null);
        foreach (var meldung in regelMeldungen)
            ZielMerken(meldung, runden, meldung.Unterzeile is int i && i < regelZeilen.Count ? regelZeilen[i].Block : null);
    }

    /// <summary>Hängt die Meldungen an Runden und Regelzeilen. <paramref name="alleBloecke"/> umfasst auch die unfertigen
    /// Regeln, die nicht geprüft wurden – ihre Zeilen verlieren alte Meldungen.</summary>
    public static RundenMeldungen Verteilen(
        IReadOnlyList<RundeZeile> runden, IReadOnlyList<Feldfehler> rundenMeldungen,
        IEnumerable<UebergangsregelBlock> alleBloecke,
        IReadOnlyList<(UebergangsregelBlock Block, UebergangsregelZeile Zeile)> regelZeilen,
        IReadOnlyList<Feldfehler> regelMeldungen)
    {
        for (int i = 0; i < runden.Count; i++)
        {
            var eigene = rundenMeldungen.Where(m => m.Zeile == i).ToList();
            runden[i].Meldungen = [.. eigene.Where(m => m.Unterzeile is null)];
            runden[i].BereichsMeldungen = [.. eigene.Where(m => m.Unterzeile is not null)];
            runden[i].FehlerAnzahl = eigene.Count + regelMeldungen.Count(m => m.Zeile == i);
        }

        foreach (var zeile in alleBloecke.SelectMany(b => b.Zeilen))
            zeile.Meldungen = [];
        for (int i = 0; i < regelZeilen.Count; i++)
            regelZeilen[i].Zeile.Meldungen = [.. regelMeldungen.Where(m => m.Unterzeile == i)];

        return new(runden, rundenMeldungen, regelZeilen, regelMeldungen);
    }

    /// <summary>Runde und gegebenenfalls Regel, die für <paramref name="meldung"/> gezeigt werden müssen.</summary>
    public (RundeZeile Runde, UebergangsregelBlock? Regel)? Ziel(Feldfehler meldung) =>
        _ziele.TryGetValue(meldung, out var ziel) ? ziel : null;

    private void ZielMerken(Feldfehler meldung, IReadOnlyList<RundeZeile> runden, UebergangsregelBlock? regel)
    {
        if (meldung.Zeile is int zeile && zeile >= 0 && zeile < runden.Count)
            _ziele[meldung] = (runden[zeile], regel);
    }
}
