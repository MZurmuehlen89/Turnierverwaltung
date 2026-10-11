using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Application.Spielplan;

public class TabellenZwischenspeicher(TabellenService tabellenService, ISpielRepository spielRepository)
{
    // Ein Durchlauf löst jeden Platzhalternamen einzeln auf — bei der CL z. B. "Platz 1 (Ligaphase)" bis
    // "Platz 24 (Ligaphase)" — und berechnete dafür jedes Mal dieselbe Quellrunden-Tabelle neu (gemessen: 24×
    // je Durchlauf, ~90 % seiner Laufzeit). Der Zwischenspeicher lebt nur für einen Durchlauf: Ergebnisse
    // ändern sich darin nicht, und trägt die Auflösung Mannschaften in eine Runde ein, wird deren Eintrag verworfen.
    private Dictionary<(int RundeId, bool NurSicher), IReadOnlyList<TabellenEintrag>>? _ligaTabellen;
    private Dictionary<int, IReadOnlyList<GruppenTabelle>>? _gruppenTabellen;
    private Dictionary<int, IReadOnlyList<(int Id, int? GruppenNummer)>>? _spielSchluessel;

    // Issue #411: auch die Spiele der Quellrunden — die K.-o.-Auflösung lud sie je Platzhaltername neu (gemessen
    // an der CL: rund 30 Ladevorgänge je Kaskadenlauf für sechs Runden). In einem Durchlauf werden keine Spiele
    // angelegt oder gelöscht, und geänderte Spiele sind dieselben verfolgten Objekte.
    private Dictionary<int, IReadOnlyList<Spiel>>? _rundenSpiele;

    /// <summary>Führt <paramref name="aktion"/> mit Tabellen-Zwischenspeicher aus. Verschachtelte Aufrufe
    /// nutzen den Speicher des äußeren Durchlaufs mit, der ihn am Ende auch wieder verwirft.</summary>
    public async Task<T> MitTabellenZwischenspeicherAsync<T>(Func<Task<T>> aktion)
    {
        if (_ligaTabellen is not null) return await aktion();

        _ligaTabellen    = [];
        _gruppenTabellen = [];
        _spielSchluessel = [];
        _rundenSpiele    = [];
        try
        {
            return await aktion();
        }
        finally
        {
            _ligaTabellen    = null;
            _gruppenTabellen = null;
            _spielSchluessel = null;
            _rundenSpiele    = null;
        }
    }

    public void TabellenZwischenspeicherVerwerfen(int rundeId)
    {
        _ligaTabellen?.Remove((rundeId, true));
        _ligaTabellen?.Remove((rundeId, false));
        _gruppenTabellen?.Remove(rundeId);
        _rundenSpiele?.Remove(rundeId);
    }

    /// <summary>Alle Spiele einer Runde, innerhalb eines Auflösungsdurchlaufs nur einmal geladen.</summary>
    public async Task<IReadOnlyList<Spiel>> RundenSpieleAsync(int rundeId, CancellationToken ct)
    {
        if (_rundenSpiele?.TryGetValue(rundeId, out var gespeichert) == true) return gespeichert;

        var spiele = await spielRepository.AlleNachRundeAsync(rundeId, ct);
        _rundenSpiele?.TryAdd(rundeId, spiele);
        return spiele;
    }

    /// <summary>Gesamttabelle einer Liga-/TopfLiga-/Schweizer-Runde — mit <see cref="TabellenEintrag.SicherPositioniert"/>
    /// (Issue #343) oder als reiner aktueller Stand für die Vorschau (Issue #208).</summary>
    public async Task<IReadOnlyList<TabellenEintrag>> LigaTabelleAsync(int rundeId, bool nurSicher, CancellationToken ct)
    {
        if (_ligaTabellen?.TryGetValue((rundeId, nurSicher), out var gespeichert) == true) return gespeichert;

        var tabelle = nurSicher
            ? await tabellenService.LigaTabelleMitSicherheitBerechnenAsync(rundeId, ct)
            : await tabellenService.TabelleBerechnenAsync(rundeId, ct: ct);
        _ligaTabellen?.TryAdd((rundeId, nurSicher), tabelle);
        return tabelle;
    }

    public async Task<IReadOnlyList<GruppenTabelle>> GruppenTabellenAsync(int rundeId, CancellationToken ct)
    {
        if (_gruppenTabellen?.TryGetValue(rundeId, out var gespeichert) == true) return gespeichert;

        var tabellen = await tabellenService.GruppenTabellenBerechnenAsync(rundeId, ct: ct);
        _gruppenTabellen?.TryAdd(rundeId, tabellen);
        return tabellen;
    }

    /// <summary>Issue #88/#389: liegt ein zur Löschung anstehendes Spiel in der Quellrunde (bei
    /// <paramref name="gruppenNummer"/> nur in dieser Gruppe; Spiele ohne Gruppennummer aus Altdaten zählen immer)?
    /// Die Kaskadenplanung stellt diese Frage je Platzhaltername — bei der CL 46× für dieselbe Ligaphase, die dafür
    /// jedes Mal komplett geladen wurde (~0,5 s vor dem Bestätigungsdialog). Spiel-IDs und Gruppen ändern sich
    /// innerhalb eines Durchlaufs nicht (es werden nur Mannschaften und Ergebnisse geschrieben, keine Spiele
    /// angelegt oder gelöscht), daher genügt ein Ladevorgang je Runde.</summary>
    public async Task<bool> EnthaeltIgnoriertesSpielAsync(
        int rundeId, IReadOnlySet<int>? ignorierteSpielIds, CancellationToken ct, int? gruppenNummer = null)
    {
        if (ignorierteSpielIds is not { Count: > 0 }) return false;

        if (_spielSchluessel?.TryGetValue(rundeId, out var spiele) != true)
            spiele = SpielSchluesselMerken(rundeId, await RundenSpieleAsync(rundeId, ct));

        return spiele!.Any(s => ignorierteSpielIds.Contains(s.Id)
            && (gruppenNummer is null || s.GruppenNummer is null || s.GruppenNummer == gruppenNummer));
    }

    /// <summary>Hält die Spiel-IDs einer ohnehin geladenen Runde für <see cref="EnthaeltIgnoriertesSpielAsync"/> vor.</summary>
    public IReadOnlyList<(int Id, int? GruppenNummer)> SpielSchluesselMerken(int rundeId, IReadOnlyList<Spiel> spiele)
    {
        var schluessel = spiele.Select(s => (s.Id, s.GruppenNummer)).ToList();
        _spielSchluessel?.TryAdd(rundeId, schluessel);
        return schluessel;
    }
}
