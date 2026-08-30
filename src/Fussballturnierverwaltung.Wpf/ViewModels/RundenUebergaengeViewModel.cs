using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

// ── Anzeige-Hilfsklassen für das Canvas-Rendering ──────────────────────────

public class RundenUebergangKarte
{
    public int TurnierRundeId { get; init; }
    public string Name { get; init; } = "";
    public string RundenartText { get; init; } = "";
    public string EinAusgangText { get; init; } = "";

    // Issue #249, Nutzerfeedback 2026-08-30: die Darstellung muss sowohl komplett fest vorgegebene
    // Turniere (Auslosungsart.Manuell) als auch (teilweise) ausgeloste gleichermaßen sinnvoll zeigen —
    // daher hier explizit als Kartenzeile statt implizit vorausgesetzt. Null, solange für die Runde noch
    // keine Auslosung angelegt wurde.
    public string? AuslosungsLabel { get; init; }
    public bool HatAuslosungsLabel => AuslosungsLabel is not null;

    // Löst #187 mit ab (Nutzerfeedback 2026-08-30, Überschneidung geprüft): eine Quellrunde kann ihren
    // Ausgang über mehrere unabhängige "Ausgehende Übergangsregel"-Blöcke auf verschiedene Zielrunden
    // aufteilen (seit #186) — in der flachen Tabellen-UI ist dabei nicht sichtbar, ob die Summe aller
    // Blöcke zusammen wirklich der Ausgangsanzahl entspricht. Hier auf einen Blick sichtbar.
    public string? UnstimmigkeitText { get; init; }
    public bool HatUnstimmigkeit => UnstimmigkeitText is not null;

    // Spaltenposition = Graphtiefe (längster Pfad ohne eingehende Übergangsregel), nicht die freie
    // Reihenfolge im Runden-Editor — sonst landen z.B. Finale und Spiel um Platz 3 (beide aus dem
    // Halbfinale) in zwei verschiedenen Spalten statt nebeneinander (Nutzerfeedback 2026-08-30).
    public double X { get; init; }
    public double Y { get; init; }
}

// Ein Übergang je (Quellrunde, Zielrunde)-Paar — mehrere Uebergangsregeln zwischen demselben Paar
// (z.B. je Gruppe eine Zeile) werden zu einer einzigen Verbindungslinie zusammengefasst, das ist auf
// dieser Übersichtsebene die sinnvolle Granularität (Detailkonfiguration bleibt im tabellarischen Editor).
public record RundenUebergangVerbindung
{
    public int QuellRundeId { get; init; }
    public int ZielRundeId { get; init; }
    public string Beschriftung { get; init; } = "";

    // Nutzerfeedback 2026-08-30: nicht jeder Übergang hat eine eigene Uebergangsregel-Zeile — der
    // Normalfall (Runde folgt direkt auf die vorherige in der Rundenliste, EingangUeberUebergangsregeln =
    // false) erzeugt gar keine, ist aber trotzdem ein echter Übergang. Optisch unterschieden (gestrichelt,
    // ohne Beschriftung) statt einer erfundenen Platzierungsangabe.
    public bool IstImplizit { get; init; }

    // Nutzerfeedback 2026-08-30 (Champions-League-Testturnier: Ligaphase → direkt Achtelfinale UND
    // Ligaphase → Playoff-Runde → Achtelfinale): überspringt eine Verbindung mindestens eine Spalte, würde
    // eine gerade Linie optisch durch die dazwischenliegende(n) Kartenspalte(n) laufen und wie eine
    // Verbindung zu einer dortigen Karte aussehen. Solche Verbindungen bekommen stattdessen eine eigene,
    // nummerierte Ausweich-Spur oberhalb der Kartenreihen (siehe CanvasBreite/-Höhe-Berechnung); null =
    // normale gerade Linie zwischen benachbarten Spalten.
    public int? Ausweichspur { get; init; }
}

public partial class RundenUebergaengeViewModel : ViewModelBase
{
    private const double CARD_W = 190.0;
    private const double CARD_H = 132.0; // großzügig für bis zu 4 Textzeilen (Name/Art/Ein-Ausgang/Auslosung+Warnung)
    private const double GAP_H = 90.0;
    private const double GAP_V = 24.0;
    // Von RundenUebergaengeView.xaml.cs zum Zeichnen der Ausweichspur-Knicke mitgenutzt.
    public const double AUSWEICHSPUR_H = 18.0; // Höhe je Ausweichspur oberhalb der Kartenreihen
    private const double AUSWEICHSPUR_RAND = 10.0; // Mindestabstand der untersten Ausweichspur zur ersten Kartenreihe

    private readonly TurnierInstanzService _instanzService;
    private readonly UebergangsregelService _uebergangsregelService;
    private readonly AuslosungService _auslosungService;
    private readonly AusgewaehlteTurnierInstanzService _turnierAuswahlService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeinTurnierGewaehlt))]
    private TurnierInstanz? _ausgewaehlteInstanz;

    public bool KeinTurnierGewaehlt => AusgewaehlteInstanz is null;

    [ObservableProperty] private bool _istLadend;
    [ObservableProperty] private string? _fehlermeldung;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatKeineRunden))]
    private ObservableCollection<RundenUebergangKarte> _karten = [];

    [ObservableProperty] private ObservableCollection<RundenUebergangVerbindung> _verbindungen = [];

    [ObservableProperty] private double _canvasBreite;
    [ObservableProperty] private double _canvasHoehe;

    public bool HatKeineRunden => !KeinTurnierGewaehlt && Karten.Count == 0;

    public RundenUebergaengeViewModel(
        TurnierInstanzService instanzService,
        UebergangsregelService uebergangsregelService,
        AuslosungService auslosungService,
        AusgewaehlteTurnierInstanzService turnierAuswahlService)
    {
        _instanzService = instanzService;
        _uebergangsregelService = uebergangsregelService;
        _auslosungService = auslosungService;
        _turnierAuswahlService = turnierAuswahlService;
    }

    public override async Task AktivierenAsync()
    {
        var instanzId = _turnierAuswahlService.TurnierInstanzId;
        if (instanzId is null)
        {
            AusgewaehlteInstanz = null;
            Karten = [];
            Verbindungen = [];
            return;
        }

        IstLadend = true;
        Fehlermeldung = null;
        try
        {
            var instanz = await _instanzService.FindenNachIdMitRundenAsync(instanzId.Value);
            AusgewaehlteInstanz = instanz;
            if (instanz is null)
            {
                Karten = [];
                Verbindungen = [];
                return;
            }

            var auslosungenNachRundeId = (await _auslosungService.AlleNachInstanzAbrufenAsync(instanz.Id))
                .ToDictionary(a => a.TurnierRundeId, a => a.Auslosungsart);
            var regeln = await _uebergangsregelService.AlleNachTurnierInstanzIdAsync(instanz.Id);

            var runden = instanz.Runden.OrderBy(r => r.Reihenfolge).ToList();

            var expliziteKanten = regeln.Select(r => (Quelle: r.QuellRundeId, Ziel: r.ZielRundeId)).Distinct().ToList();

            // Der Normalfall: eine Runde ohne eigene Uebergangsregel folgt einfach direkt auf die
            // vorherige Runde in der Liste (EingangUeberUebergangsregeln = false) — das ist strukturell
            // genauso ein Übergang wie eine explizite Regel und muss in den Graphen einfließen, sonst
            // stapeln sich alle "einfachen" Runden undifferenziert in Spalte 0 (Nutzerfeedback 2026-08-30).
            var impliziteKanten = new List<(int Quelle, int Ziel)>();
            for (int i = 0; i < runden.Count - 1; i++)
            {
                var vorgaenger = runden[i];
                var nachfolger = runden[i + 1];
                if (!nachfolger.EingangUeberUebergangsregeln && !expliziteKanten.Contains((vorgaenger.Id, nachfolger.Id)))
                    impliziteKanten.Add((vorgaenger.Id, nachfolger.Id));
            }

            var alleKanten = expliziteKanten.Concat(impliziteKanten).ToList();
            var tiefen = TiefenBerechnen(runden, alleKanten);

            var expliziteVerbindungen = regeln
                .GroupBy(r => (r.QuellRundeId, r.ZielRundeId))
                .Select(g => new RundenUebergangVerbindung
                {
                    QuellRundeId = g.Key.QuellRundeId,
                    ZielRundeId = g.Key.ZielRundeId,
                    Beschriftung = BeschriftungErstellen(g.ToList())
                });
            var impliziteVerbindungen = impliziteKanten
                .Select(k => new RundenUebergangVerbindung { QuellRundeId = k.Quelle, ZielRundeId = k.Ziel, IstImplizit = true });

            var verbindungen = expliziteVerbindungen.Concat(impliziteVerbindungen).ToList();
            var ausweichspurenVergeben = 0;
            for (int i = 0; i < verbindungen.Count; i++)
            {
                var v = verbindungen[i];
                // Überspringt die Verbindung mindestens eine Spalte, würde eine gerade Linie optisch durch
                // die Zwischenspalte(n) laufen (Nutzerfeedback 2026-08-30, CL-Testturnier: Ligaphase →
                // direkt Achtelfinale UND Ligaphase → Playoff-Runde → Achtelfinale).
                if (tiefen[v.ZielRundeId] - tiefen[v.QuellRundeId] > 1)
                    verbindungen[i] = v with { Ausweichspur = ausweichspurenVergeben++ };
            }
            Verbindungen = new ObservableCollection<RundenUebergangVerbindung>(verbindungen);

            var kartenObenRand = ausweichspurenVergeben == 0 ? 0.0 : ausweichspurenVergeben * AUSWEICHSPUR_H + AUSWEICHSPUR_RAND;

            var karten = new List<RundenUebergangKarte>();
            foreach (var spalte in runden.GroupBy(r => tiefen[r.Id]).OrderBy(g => g.Key))
            {
                var geordnete = spalte.OrderBy(r => r.Reihenfolge).ToList();
                for (int zeile = 0; zeile < geordnete.Count; zeile++)
                {
                    var runde = geordnete[zeile];
                    karten.Add(KarteErstellen(
                        runde,
                        auslosungenNachRundeId.TryGetValue(runde.Id, out var art) ? art : null,
                        regeln,
                        x: spalte.Key * (CARD_W + GAP_H),
                        y: kartenObenRand + zeile * (CARD_H + GAP_V)));
                }
            }
            Karten = new ObservableCollection<RundenUebergangKarte>(karten);

            var anzahlSpalten = runden.Count == 0 ? 0 : tiefen.Values.Max() + 1;
            var maxZeilenProSpalte = runden.Count == 0 ? 0 : runden.GroupBy(r => tiefen[r.Id]).Max(g => g.Count());
            CanvasBreite = anzahlSpalten * (CARD_W + GAP_H);
            CanvasHoehe = kartenObenRand + maxZeilenProSpalte * (CARD_H + GAP_V);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            IstLadend = false;
        }
    }

    /// <summary>Graphtiefe je Runde = längster Pfad eingehender Übergänge (0 = keine Vorgänger) — sowohl
    /// explizite Uebergangsregeln als auch die implizite Listenreihenfolge-Kette (siehe AktivierenAsync)
    /// zählen als Kante. Bestimmt die Spaltenposition, damit z.B. Finale und Spiel um Platz 3 (beide aus
    /// demselben Halbfinale) in derselben Spalte nebeneinander landen statt in zwei verschiedenen Spalten,
    /// die sich nur zufällig aus der Reihenfolge im Runden-Editor ergeben würden.</summary>
    private static Dictionary<int, int> TiefenBerechnen(IReadOnlyList<TurnierRunde> runden, IReadOnlyList<(int Quelle, int Ziel)> kanten)
    {
        var quellenNachZiel = kanten
            .GroupBy(k => k.Ziel)
            .ToDictionary(g => g.Key, g => g.Select(k => k.Quelle).Distinct().ToList());

        var tiefen = new Dictionary<int, int>();
        var inBearbeitung = new HashSet<int>();

        int TiefeErmitteln(int rundeId)
        {
            if (tiefen.TryGetValue(rundeId, out var bekannt)) return bekannt;
            // Zyklus-Schutz: kann bei konsistenten Daten nicht vorkommen (UebergangsregelValidator
            // verhindert zyklische Übergänge beim Speichern), defensiv trotzdem abgesichert.
            if (!inBearbeitung.Add(rundeId)) return 0;

            var quellen = quellenNachZiel.TryGetValue(rundeId, out var q) ? q : [];
            var tiefe = quellen.Count == 0 ? 0 : quellen.Max(TiefeErmitteln) + 1;

            inBearbeitung.Remove(rundeId);
            tiefen[rundeId] = tiefe;
            return tiefe;
        }

        foreach (var runde in runden)
            TiefeErmitteln(runde.Id);

        return tiefen;
    }

    private static RundenUebergangKarte KarteErstellen(
        TurnierRunde runde, Auslosungsart? auslosungsart, IReadOnlyList<Uebergangsregel> alleRegeln, double x, double y) => new()
    {
        TurnierRundeId = runde.Id,
        Name = runde.Name,
        RundenartText = RundenartText(runde.Rundenart, runde.GruppenAnzahl),
        EinAusgangText = $"{runde.EingangsMannschaftsanzahl} → {runde.AusgangsMannschaftsanzahl}",
        AuslosungsLabel = AuslosungsLabel(auslosungsart),
        UnstimmigkeitText = UnstimmigkeitErmitteln(runde, alleRegeln),
        X = x,
        Y = y
    };

    private static string RundenartText(Rundenart art, int? gruppenAnzahl) => art switch
    {
        Rundenart.Gruppenphase => gruppenAnzahl is > 0 ? $"Gruppenphase ({gruppenAnzahl} Gruppen)" : "Gruppenphase",
        Rundenart.Liga => "Liga",
        Rundenart.TopfLiga => "Liga (Topf)",
        Rundenart.SchweizerSystem => "Schweizer System",
        Rundenart.KoRunde => "K.o.-Runde",
        Rundenart.Finale => "Finale",
        Rundenart.SpielUmPlatzDrei => "Spiel um Platz 3",
        Rundenart.Sonderrunde => "Sonderrunde",
        _ => art.ToString()
    };

    // Unterscheidet komplett fest vorgegebene Turniere (Manuell) von echter Auslosung, ohne die
    // Ausdrucksweise "Losung" für Manuell-Runden zu verwenden (Nutzerfeedback 2026-08-30).
    private static string? AuslosungsLabel(Auslosungsart? art) => art switch
    {
        null => null,
        Auslosungsart.Manuell => "Fest vorgegeben",
        Auslosungsart.Zufall => "Auslosung: Zufall",
        Auslosungsart.NachSetzliste => "Auslosung: Setzliste",
        Auslosungsart.NachToepfen => "Auslosung: Töpfe",
        Auslosungsart.TopfZuordnung => "Auslosung: Topfzuordnung",
        Auslosungsart.GruppensiegerGegenGruppenzweite => "Auslosung: Sieger vs. Zweite",
        Auslosungsart.Benutzerdefiniert => "Auslosung: benutzerdefiniert",
        _ => "Auslosung"
    };

    private static string BeschriftungErstellen(IReadOnlyList<Uebergangsregel> regeln)
    {
        if (regeln.Count == 1)
        {
            var r = regeln[0];
            if (r.AnzahlBeste is { } n) return $"beste {n} (Platz {r.PlatzierungVon})";
            var bereich = r.PlatzierungVon == r.PlatzierungBis ? $"Platz {r.PlatzierungVon}" : $"Platz {r.PlatzierungVon}-{r.PlatzierungBis}";
            return r.GruppeIndex is { } g ? $"{bereich} (Gr. {(char)('A' + g - 1)})" : bereich;
        }
        return $"{regeln.Count} Regeln";
    }

    private static bool IstKoRundenart(Rundenart art) =>
        art is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    /// <summary>Issue #187: prüft, ob die Summe ALLER ausgehenden Übergangsregeln einer Quellrunde (über
    /// ggf. mehrere unabhängige Blöcke zu verschiedenen Zielrunden hinweg) tatsächlich deren Ausgangsanzahl
    /// ergibt — das prüft der bestehende UebergangsregelValidator bewusst nur pro Zielrunde (Eingangs-
    /// Kapazität), nicht pro Quellrunde. Nur relevant, wenn überhaupt ausgehende Regeln existieren — eine
    /// Runde ganz ohne ausgehende Regeln (z.B. das Finale) ist kein Fehlerfall.</summary>
    private static string? UnstimmigkeitErmitteln(TurnierRunde runde, IReadOnlyList<Uebergangsregel> alleRegeln)
    {
        var ausgehende = alleRegeln.Where(r => r.QuellRundeId == runde.Id).ToList();
        if (ausgehende.Count == 0) return null;

        var summe = ausgehende.Sum(r => r.AnzahlBeste ?? (r.PlatzierungBis - r.PlatzierungVon + 1));
        var erwartet = IstKoRundenart(runde.Rundenart) ? runde.EingangsMannschaftsanzahl / 2 : runde.AusgangsMannschaftsanzahl;

        if (summe == erwartet) return null;
        return summe < erwartet
            ? $"Übergangsregeln decken nur {summe} von {erwartet} Plätzen ab — Lücke."
            : $"Übergangsregeln decken {summe} von {erwartet} Plätzen ab — mehr als erwartet.";
    }
}
