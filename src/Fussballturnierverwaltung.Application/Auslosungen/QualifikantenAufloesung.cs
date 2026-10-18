using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Die Mannschaften hinter den Platzhaltern einer Runde („1. Gruppe A“ → FC Bayern), soweit sie sicher
/// feststehen (#483). Die Auslosung arbeitet intern weiter mit den Platzhaltern — Kaskade, Gruppen-Herkunft und
/// Spielplan-Erzeugung hängen an ihnen —, zeigt und prüft aber die Mannschaft.</summary>
public sealed record Qualifikanten(IReadOnlyDictionary<string, Mannschaft> JePlatzhalter, int AnzahlOffen)
{
    public static Qualifikanten Keine { get; } = new(new Dictionary<string, Mannschaft>(), 0);

    /// <summary>Ob jeder angefragte Platzhalter aufgelöst ist.</summary>
    public bool AlleFest => AnzahlOffen == 0;

    public Mannschaft? Fuer(string? platzhalterName) =>
        platzhalterName is not null && JePlatzhalter.TryGetValue(platzhalterName, out var mannschaft) ? mannschaft : null;
}

/// <summary>Löst die Platzhalter einer Runde über <see cref="PlatzhalterAufloesung"/> auf — nur sicher feststehende
/// Plätze, wie beim Eintragen in die Spiele. Rein lesend, ein Durchlauf des Zwischenspeichers je Aufruf.</summary>
public class QualifikantenAufloesung(
    PlatzhalterAufloesung aufloesung,
    TabellenZwischenspeicher zwischenspeicher,
    ITurnierRundeRepository rundeRepository,
    IMannschaftRepository mannschaftRepository)
{
    private static readonly IReadOnlySet<int> _keineIgnoriertenSpiele = new HashSet<int>();

    public Task<Qualifikanten> AufloesenAsync(
        int turnierRundeId, IEnumerable<string> platzhalterNamen, CancellationToken ct = default)
    {
        var namen = platzhalterNamen.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return namen.Count == 0
            ? Task.FromResult(Qualifikanten.Keine)
            : zwischenspeicher.DurchlaufAsync(() => ImDurchlaufAufloesenAsync(turnierRundeId, namen, ct));
    }

    private async Task<Qualifikanten> ImDurchlaufAufloesenAsync(
        int turnierRundeId, IReadOnlyList<string> namen, CancellationToken ct)
    {
        var mannschaftIdJeName = await MannschaftIdsAufloesenAsync(turnierRundeId, namen, ct);
        var mannschaften = (await mannschaftRepository.FindenNachIdsAsync([.. mannschaftIdJeName.Values.Distinct()], ct))
            .ToDictionary(m => m.Id);

        var jePlatzhalter = new Dictionary<string, Mannschaft>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, id) in mannschaftIdJeName)
            if (mannschaften.TryGetValue(id, out var mannschaft)) jePlatzhalter[name] = mannschaft;

        return new Qualifikanten(jePlatzhalter, namen.Count - jePlatzhalter.Count);
    }

    private async Task<Dictionary<string, int>> MannschaftIdsAufloesenAsync(
        int turnierRundeId, IReadOnlyList<string> namen, CancellationToken ct)
    {
        var runde = await rundeRepository.FindenNachIdAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde mit ID {turnierRundeId} nicht gefunden.");
        var alleRunden = await rundeRepository.AlleNachInstanzAsync(runde.TurnierInstanzId, ct);

        var ergebnis = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in namen)
            if (await aufloesung.NameAufloesenAsync(name, runde, alleRunden, _keineIgnoriertenSpiele, ct) is { } id)
                ergebnis[name] = id;
        return ergebnis;
    }
}
