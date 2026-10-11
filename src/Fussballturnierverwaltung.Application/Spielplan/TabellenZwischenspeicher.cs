using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Spielplan;

/// <summary>Merkt für die Dauer eines Auflösungsdurchlaufs die Tabellen und Spiele der Quellrunden (Issue #386).
/// Außerhalb eines Durchlaufs wird nichts gemerkt — jede Abfrage geht dann an die Datenbank. Platzhalter-Auflösung
/// und Kaskade teilen sich eine Instanz je Scope, damit ein verschachtelter Durchlauf den äußeren mitnutzt.</summary>
public class TabellenZwischenspeicher(TabellenService tabellenService, ISpielRepository spielRepository)
{
    private sealed class Durchlauf
    {
        // Ein Durchlauf löst jeden Platzhalternamen einzeln auf — bei der CL z. B. "Platz 1 (Ligaphase)" bis
        // "Platz 24 (Ligaphase)" — und berechnete dafür jedes Mal dieselbe Quellrunden-Tabelle neu (gemessen: 24×
        // je Durchlauf, ~90 % seiner Laufzeit). Ergebnisse ändern sich in einem Durchlauf nicht, und trägt die
        // Auflösung Mannschaften in eine Runde ein, wird deren Eintrag verworfen.
        public Dictionary<(int RundeId, bool NurSicher), IReadOnlyList<TabellenEintrag>> LigaTabellen { get; } = [];
        public Dictionary<int, IReadOnlyList<GruppenTabelle>> GruppenTabellen { get; } = [];

        // Issue #411: auch die Spiele der Quellrunden — die K.-o.-Auflösung lud sie je Platzhaltername neu (gemessen
        // an der CL: rund 30 Ladevorgänge je Kaskadenlauf für sechs Runden). In einem Durchlauf werden keine Spiele
        // angelegt oder gelöscht, und geänderte Spiele sind dieselben verfolgten Objekte.
        public Dictionary<int, IReadOnlyList<Spiel>> RundenSpiele { get; } = [];

        // Issue #389: Spiel-IDs und Gruppen ändern sich in einem Durchlauf nicht (es werden nur Mannschaften und
        // Ergebnisse geschrieben) — sie bleiben deshalb auch stehen, wenn die Spiele ihrer Runde verworfen werden.
        public Dictionary<int, IReadOnlyList<(int Id, int? GruppenNummer)>> SpielSchluessel { get; } = [];
    }

    private Durchlauf? _durchlauf;

    /// <summary>Führt <paramref name="aktion"/> als Durchlauf aus. Verschachtelte Aufrufe nutzen den Speicher
    /// des äußeren Durchlaufs mit, der ihn am Ende auch wieder verwirft.</summary>
    public async Task<T> DurchlaufAsync<T>(Func<Task<T>> aktion)
    {
        if (_durchlauf is not null) return await aktion();

        _durchlauf = new Durchlauf();
        try
        {
            return await aktion();
        }
        finally
        {
            _durchlauf = null;
        }
    }

    /// <summary>Für den Aufrufer, der im Durchlauf Mannschaften in die Spiele einer Runde eingetragen hat.</summary>
    public void Verwerfen(int rundeId)
    {
        _durchlauf?.LigaTabellen.Remove((rundeId, true));
        _durchlauf?.LigaTabellen.Remove((rundeId, false));
        _durchlauf?.GruppenTabellen.Remove(rundeId);
        _durchlauf?.RundenSpiele.Remove(rundeId);
    }

    /// <summary>Alle Spiele einer Runde, innerhalb eines Auflösungsdurchlaufs nur einmal geladen.</summary>
    public async Task<IReadOnlyList<Spiel>> RundenSpieleAsync(int rundeId, CancellationToken ct)
    {
        if (_durchlauf?.RundenSpiele.TryGetValue(rundeId, out var gemerkt) == true) return gemerkt;

        var spiele = await spielRepository.AlleNachRundeAsync(rundeId, ct);
        _durchlauf?.RundenSpiele.TryAdd(rundeId, spiele);
        _durchlauf?.SpielSchluessel.TryAdd(rundeId, Schluessel(spiele));
        return spiele;
    }

    /// <summary>Gesamttabelle einer Liga-/TopfLiga-/Schweizer-Runde — mit <see cref="TabellenEintrag.SicherPositioniert"/>
    /// (Issue #343) oder als reiner aktueller Stand für die Vorschau (Issue #208).</summary>
    public async Task<IReadOnlyList<TabellenEintrag>> LigaTabelleAsync(int rundeId, bool nurSicher, CancellationToken ct)
    {
        if (_durchlauf?.LigaTabellen.TryGetValue((rundeId, nurSicher), out var gemerkt) == true) return gemerkt;

        var tabelle = nurSicher
            ? await tabellenService.LigaTabelleMitSicherheitBerechnenAsync(rundeId, ct)
            : await tabellenService.TabelleBerechnenAsync(rundeId, ct: ct);
        _durchlauf?.LigaTabellen.TryAdd((rundeId, nurSicher), tabelle);
        return tabelle;
    }

    public async Task<IReadOnlyList<GruppenTabelle>> GruppenTabellenAsync(int rundeId, CancellationToken ct)
    {
        if (_durchlauf?.GruppenTabellen.TryGetValue(rundeId, out var gemerkt) == true) return gemerkt;

        var tabellen = await tabellenService.GruppenTabellenBerechnenAsync(rundeId, ct: ct);
        _durchlauf?.GruppenTabellen.TryAdd(rundeId, tabellen);
        return tabellen;
    }

    /// <summary>Issue #88/#389: liegt ein zur Löschung anstehendes Spiel in der Quellrunde (bei
    /// <paramref name="gruppenNummer"/> nur in dieser Gruppe; Spiele ohne Gruppennummer aus Altdaten zählen immer)?
    /// Die Kaskadenplanung stellt diese Frage je Platzhaltername — bei der CL 46× für dieselbe Ligaphase, die dafür
    /// jedes Mal komplett geladen wurde (~0,5 s vor dem Bestätigungsdialog); deshalb genügt ein Ladevorgang je Runde.</summary>
    public async Task<bool> EnthaeltIgnoriertesSpielAsync(
        int rundeId, IReadOnlySet<int>? ignorierteSpielIds, CancellationToken ct, int? gruppenNummer = null)
    {
        if (ignorierteSpielIds is not { Count: > 0 }) return false;

        if (_durchlauf?.SpielSchluessel.TryGetValue(rundeId, out var schluessel) != true)
            schluessel = Schluessel(await RundenSpieleAsync(rundeId, ct));

        return schluessel!.Any(s => ignorierteSpielIds.Contains(s.Id)
            && (gruppenNummer is null || s.GruppenNummer is null || s.GruppenNummer == gruppenNummer));
    }

    private static IReadOnlyList<(int Id, int? GruppenNummer)> Schluessel(IReadOnlyList<Spiel> spiele) =>
        [.. spiele.Select(s => (s.Id, s.GruppenNummer))];
}
