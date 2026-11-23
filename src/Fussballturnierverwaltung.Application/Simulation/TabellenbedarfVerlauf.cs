using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Was zu einer Runde mit Tabelle gehört, damit ihr Bedarf berechnet werden kann: ihre Zonen und das Punktesystem.</summary>
public sealed record TabellenbedarfGrundlage(IReadOnlyList<TabellenZone> Zonen, PunkteRegel Punkte);

/// <summary>
/// Der Tabellenstand eines Turniers während eines Simulationsvorgangs (#176): hält je Gruppe, was die Spiele bisher gebracht haben,
/// und liefert für das nächste Spiel den <see cref="Tabellenbedarf"/> beider Teams aus dem Stand <b>vor</b> dessen Spieltag –
/// Spiele desselben Spieltags zählen nicht, die Simulationsreihenfolge innerhalb des Spieltags ändert nichts. Gelesen wird der
/// Bestand einmal beim Anlegen; was der Vorgang simuliert, kommt über <see cref="Setzen"/> dazu. Die Rechnung selbst steckt in
/// <see cref="TabellenbedarfRechner"/>. Runden ohne Zonen, Spiele ohne Spieltag und Runden mit noch offenen Platzhaltern kennen
/// keinen Bedarf (<see cref="Tabellenbedarf.Offen"/>).
/// </summary>
public sealed class TabellenbedarfVerlauf
{
    private readonly Dictionary<int, Gruppe> _gruppeJeSpiel = [];
    private readonly Dictionary<int, (int Heim, int Gast)> _ergebnisJeSpiel = [];

    public TabellenbedarfVerlauf(IEnumerable<Spiel> spiele, IReadOnlyDictionary<int, TabellenbedarfGrundlage> grundlagen)
    {
        foreach (var rundenSpiele in spiele.GroupBy(s => s.TurnierRundeId))
        {
            if (!grundlagen.TryGetValue(rundenSpiele.Key, out var grundlage)) continue;
            var liste = rundenSpiele.ToList();
            if (!Vollstaendig(liste)) continue;

            foreach (var (nummer, gruppenSpiele) in Gruppen(liste))
            {
                var gruppe = new Gruppe(nummer, grundlage, gruppenSpiele);
                foreach (var spiel in gruppenSpiele)
                    _gruppeJeSpiel[spiel.Id] = gruppe;
            }
            foreach (var spiel in liste.Where(TabellenRechner.HatErgebnis))
                _ergebnisJeSpiel[spiel.Id] = (spiel.Ergebnis!.HeimToreGesamt, spiel.Ergebnis.AuswaertsToreGesamt);
        }
    }

    /// <summary>Der Bedarf beider Teams vor diesem Spiel; ohne Tabellenkontext beide <see cref="Tabellenbedarf.Offen"/>.</summary>
    public TabellenbedarfJeTeam BedarfFuer(Spiel spiel)
    {
        if (!_gruppeJeSpiel.TryGetValue(spiel.Id, out var gruppe) || spiel.Spieltag is not int spieltag
            || spiel.HeimMannschaftId is not int heimId || spiel.AuswaertsMannschaftId is not int gastId)
            return default;

        var stand = gruppe.StandVor(spieltag, _ergebnisJeSpiel);
        return new TabellenbedarfJeTeam(Bedarf(heimId, stand, gruppe), Bedarf(gastId, stand, gruppe));
    }

    /// <summary>Merkt das Ergebnis, das der Vorgang für das Spiel gewürfelt hat; spätere Spieltage rechnen damit neu.</summary>
    public void Setzen(Spiel spiel, SimulationsErgebnis ergebnis)
    {
        if (!_gruppeJeSpiel.TryGetValue(spiel.Id, out var gruppe)) return;

        _ergebnisJeSpiel[spiel.Id] = (
            ergebnis.HeimToreRegulaer + (ergebnis.HeimToreVerlaengerung ?? 0),
            ergebnis.AuswaertsToreRegulaer + (ergebnis.AuswaertsToreVerlaengerung ?? 0));
        gruppe.StaendeNachSpieltagVerwerfen(spiel.Spieltag ?? 0);
    }

    private static Tabellenbedarf Bedarf(int mannschaftId, IReadOnlyList<Tabellenstand> stand, Gruppe gruppe) =>
        TabellenbedarfRechner.Berechnen(mannschaftId, stand, gruppe.Grundlage.Zonen, gruppe.Nummer, gruppe.Grundlage.Punkte);

    /// <summary>Alle Spiele der Runde haben beide Teams und einen Spieltag – sonst fehlt der Rest der Runde (offene Platzhalter)
    /// oder der Zeitpunkt, und kein Stand ließe sich zuverlässig bestimmen.</summary>
    private static bool Vollstaendig(IReadOnlyList<Spiel> spiele) =>
        spiele.All(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue && s.Spieltag.HasValue);

    /// <summary>Eine Gruppenphase zerfällt in ihre Gruppen, jede andere Runde ist eine einzige Tabelle ohne Gruppennummer.</summary>
    private static IEnumerable<(int? Nummer, IReadOnlyList<Spiel> Spiele)> Gruppen(IReadOnlyList<Spiel> rundenSpiele) =>
        rundenSpiele[0].TurnierRunde?.Rundenart == Rundenart.Gruppenphase
            ? GruppenRechner.GruppenErmitteln(rundenSpiele).Select(g => ((int?)g.Nummer, g.Spiele))
            : [(null, rundenSpiele)];

    /// <summary>Eine Tabelle der Runde: ihre Spiele und die Stände vor jedem Spieltag, einmal gerechnet und bei einem neuen
    /// Ergebnis für spätere Spieltage verworfen.</summary>
    private sealed class Gruppe(int? nummer, TabellenbedarfGrundlage grundlage, IReadOnlyList<Spiel> spiele)
    {
        private readonly Dictionary<int, Tabellenstand[]> _standVorSpieltag = [];

        public int? Nummer { get; } = nummer;
        public TabellenbedarfGrundlage Grundlage { get; } = grundlage;

        public void StaendeNachSpieltagVerwerfen(int spieltag)
        {
            foreach (int key in _standVorSpieltag.Keys.Where(k => k > spieltag).ToList())
                _standVorSpieltag.Remove(key);
        }

        /// <summary>Punkte aus allen Spielen mit Ergebnis an früheren Spieltagen; offen ist, was davon noch nicht gespielt ist –
        /// auch ein früheres Spiel ohne Ergebnis.</summary>
        public Tabellenstand[] StandVor(int spieltag, IReadOnlyDictionary<int, (int Heim, int Gast)> ergebnisse)
        {
            if (_standVorSpieltag.TryGetValue(spieltag, out var stand)) return stand;

            var punkte = new Dictionary<int, int>();
            var offen = new Dictionary<int, int>();
            foreach (var spiel in spiele)
            {
                int heim = spiel.HeimMannschaftId!.Value, gast = spiel.AuswaertsMannschaftId!.Value;
                offen[heim] = offen.GetValueOrDefault(heim) + 1;
                offen[gast] = offen.GetValueOrDefault(gast) + 1;
                punkte.TryAdd(heim, 0);
                punkte.TryAdd(gast, 0);
                if (spiel.Spieltag >= spieltag || !ergebnisse.TryGetValue(spiel.Id, out var tore)) continue;

                punkte[heim] += PunkteFuer(tore.Heim, tore.Gast);
                punkte[gast] += PunkteFuer(tore.Gast, tore.Heim);
                offen[heim]--;
                offen[gast]--;
            }
            return _standVorSpieltag[spieltag] = [.. punkte.Select(kv => new Tabellenstand(kv.Key, kv.Value, offen[kv.Key]))];
        }

        private int PunkteFuer(int eigeneTore, int gegnerTore) =>
            eigeneTore > gegnerTore ? Grundlage.Punkte.PunkteFuerSieg
            : eigeneTore == gegnerTore ? Grundlage.Punkte.PunkteFuerUnentschieden
            : Grundlage.Punkte.PunkteFuerNiederlage;
    }
}
