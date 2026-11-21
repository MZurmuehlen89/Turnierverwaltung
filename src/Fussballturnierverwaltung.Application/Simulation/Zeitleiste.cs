using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Issue #237: Trenner-Untertyp fürs Rendering der Zeitleisten-Grafik — Abschnitt/Elfmeter
/// durchbrechen die Mittelachse mit einer Linie, Start/Ende/Beinabschnitt stehen als eigenständige
/// Marke ohne Linie (Anpfiff/Abpfiff bzw. HINSPIEL/RÜCKSPIEL-Kopf bei Hin- und Rückspiel).</summary>
public enum TrennerArt { Abschnitt, Elfmeter, Start, Ende, Beinabschnitt }

public record EinzelspielEreignisZeile(
    string Minute, string Seite, string Art, bool IstHeimSeite,
    bool IstTrenner = false, string? TrennerText = null, TrennerArt TrennerArt = TrennerArt.Abschnitt,
    byte[]? Wappen = null,
    bool IstTor = false, bool IstGelbeKarte = false, bool IstRoteKarte = false, bool IstElfmeterVerschossen = false,
    string? Detail = null, int MinuteWert = 0, bool IstElfmeterschuss = false, bool IstTaktikwechsel = false,
    bool IstGrosschance = false)
{
    // Nur fuer IstTrenner==true aussagekräftig — die beiden Properties kombinieren beides, damit die
    // Zeitleisten-DataTemplate-Grids nicht zusätzlich auf IstTrenner prüfen müssen (sonst würde bei
    // TrennerArt-Default=Abschnitt jede normale Ereignis-Zeile fälschlich als Abschnittstrenner gelten).
    public bool ZeigtAlsAbschnittMitLinie   => IstTrenner && TrennerArt is TrennerArt.Abschnitt or TrennerArt.Elfmeter;
    public bool ZeigtAlsEigenstaendigeMarke => IstTrenner && TrennerArt is TrennerArt.Start or TrennerArt.Ende or TrennerArt.Beinabschnitt;

    // WinUI3 kennt kein Style.Triggers/DataTrigger (anders als das WPF-Original) — die Abschnitt-Pille
    // wechselt bei Elfmeterschießen die Farbe daher über eine eigene Bool-Property statt eines
    // TrennerArt-DataTriggers im XAML (siehe SpielberichtControl.xaml).
    public bool IstElfmeterTrenner => TrennerArt == TrennerArt.Elfmeter;

    // Issue #341: minuteWert = Zeitpunkt des Abschnittsendes (45/90/105/120) — der kompakte Zeitstrahl
    // (SpielZeitstrahlControl) erkennt eine gespielte Verlängerung an Trennern jenseits der 90. Minute.
    public static EinzelspielEreignisZeile Trenner(string text, TrennerArt art = TrennerArt.Abschnitt, int minuteWert = 0) =>
        new("", "", "", false, IstTrenner: true, TrennerText: text, TrennerArt: art, MinuteWert: minuteWert);
}

/// <summary>Ein Spielereignis als Eingabe der Zeitleiste (#509) – unabhängig davon, ob es frisch simuliert
/// oder aus der Datenbank geladen wurde.</summary>
public sealed record ZeitleistenEreignis(
    int Minute, int? Nachspielminute, EreignisArt Art, bool IstHeimSeite, string Beschreibung, bool ImWiederholungsspiel)
{
    public static ZeitleistenEreignis Aus(SimuliertesEreignis e) =>
        new(e.Minute, e.Nachspielminute, e.Art, e.IstHeim, e.Beschreibung, e.ImWiederholungsspiel);
}

/// <summary>Baut die Ereigniszeitleiste eines Spiels inkl. Abschnittstrenner (Halbzeit, 90 Minuten,
/// Halbzeit/Ende Verlängerung, Elfmeterschießen) und ANPFIFF/ABPFIFF-Marken (#237, #509).</summary>
public static class Zeitleiste
{
    /// <summary>Die Zeilen der Zeitleiste des (ersten) Spiels in Anzeigereihenfolge; ohne Ereignisse leer (die
    /// Schnellsimulation persistiert keine Einzelereignisse – sonst stünde dort ein vorgetäuschter HALBZEIT-Trenner).
    /// Die Spielstände kommen aus <paramref name="stand"/>; dessen <c>Ereignisse</c> werden nicht gelesen. Das
    /// Wiederholungsspiel hat eine eigene Uhr und eine eigene Zeitleiste (<see cref="ErstellenWiederholungsspiel"/>,
    /// #247) – seine Ereignisse gehören nicht hierher. Ereignisse eines Wiederholungsspiels ohne gespeichertes
    /// Wiederholungsergebnis kann die App nicht erzeugen (Engine und Eingabe setzen beides zusammen); bei solchen
    /// Bestandsdaten fehlt die Karte des Wiederholungsspiels.</summary>
    public static IReadOnlyList<EinzelspielEreignisZeile> Erstellen(
        SimulationsErgebnis stand, IEnumerable<ZeitleistenEreignis> ereignisse, SpielSeite heim, SpielSeite auswaerts) =>
        Aufbauen(stand, ereignisse.Where(ev => !ev.ImWiederholungsspiel).ToList(), heim, auswaerts);

    /// <summary>Die Zeitleiste des Wiederholungsspiels (#247) mit eigenem Anpfiff bei Minute 0 und eigenen Trennern.
    /// <paramref name="heim"/>/<paramref name="auswaerts"/> sind die Seiten des ersten Spiels, die Ereignisse stehen –
    /// wie <paramref name="wiederholung"/> – in dessen Sicht; bei <paramref name="getauscht"/> wird gespiegelt, damit das
    /// tatsächliche Heimteam des Wiederholungsspiels links steht.</summary>
    public static IReadOnlyList<EinzelspielEreignisZeile> ErstellenWiederholungsspiel(
        Wiederholungsergebnis wiederholung, IEnumerable<ZeitleistenEreignis> ereignisse, SpielSeite heim, SpielSeite auswaerts, bool getauscht)
    {
        var eigene = ereignisse
            .Where(ev => ev.ImWiederholungsspiel)
            .Select(ev => getauscht ? ev with { IstHeimSeite = !ev.IstHeimSeite } : ev)
            .ToList();
        return getauscht
            ? Aufbauen(wiederholung.AlsErgebnis(true), eigene, auswaerts, heim)
            : Aufbauen(wiederholung.AlsErgebnis(false), eigene, heim, auswaerts);
    }

    private static IReadOnlyList<EinzelspielEreignisZeile> Aufbauen(
        SimulationsErgebnis stand, List<ZeitleistenEreignis> ereignisse, SpielSeite heim, SpielSeite auswaerts)
    {
        if (ereignisse.Count == 0) return [];

        var aufbau = new Aufbau(stand, heim, auswaerts);
        aufbau.Anpfiff();
        aufbau.Hauptspiel(ereignisse);
        aufbau.SchwellenPruefen(int.MaxValue); // noch nicht erreichte Schwellen nachtragen
        aufbau.Elfmetertrenner();
        aufbau.Abpfiff();
        return aufbau.Zeilen;
    }

    /// <summary>Der Zustand eines Aufbaus: die Zeilen bisher, welche Abschnittstrenner schon stehen, der laufende Stand.</summary>
    private sealed class Aufbau(SimulationsErgebnis stand, SpielSeite heim, SpielSeite auswaerts)
    {
        // Verlaengerung fand statt, sobald die Verlaengerungs-Torfelder gesetzt sind — unabhaengig
        // von der finalen EntscheidungArt (auch bei Elfmeterschiessen ging dem immer eine
        // vollstaendige Verlaengerung voraus, siehe ErgebnisEntscheidung.Art).
        private readonly bool _hatVerlaengerung = stand is { HeimToreVerlaengerung: not null, AuswaertsToreVerlaengerung: not null };
        private readonly bool _hatElfmeter = stand.EntscheidungArt == EntscheidungArt.Elfmeterschiessen;

        private bool _halbzeit, _neunzig, _verlaengerungHalbzeit, _verlaengerungEnde;
        private int _heimStand, _auswaertsStand;

        public List<EinzelspielEreignisZeile> Zeilen { get; } = [];

        public void Anpfiff() => Zeilen.Add(EinzelspielEreignisZeile.Trenner("ANPFIFF", TrennerArt.Start));

        // #361: Nachspielzeit („90+4'“) nach der regulären Minute, Ereignisse derselben Minute in Entstehungsreihenfolge.
        public void Hauptspiel(IEnumerable<ZeitleistenEreignis> ereignisse)
        {
            foreach (var ereignis in ereignisse.Chronologisch(ev => ev.Minute, ev => ev.Nachspielminute))
            {
                SchwellenPruefen(ereignis.Minute);

                if (VeraendertSpielstand(ereignis.Art))
                {
                    if (ereignis.IstHeimSeite) _heimStand++; else _auswaertsStand++;
                }

                Zeilen.Add(Zeile(ereignis));
            }
        }

        public void SchwellenPruefen(int minute)
        {
            if (!_halbzeit && minute > 45)
            {
                Zeilen.Add(EinzelspielEreignisZeile.Trenner($"HALBZEIT · {_heimStand}:{_auswaertsStand}", minuteWert: 45));
                _halbzeit = true;
            }
            if (!_neunzig && (_hatVerlaengerung || _hatElfmeter) && minute > 90)
            {
                Zeilen.Add(EinzelspielEreignisZeile.Trenner($"90 MINUTEN · {stand.HeimToreRegulaer}:{stand.AuswaertsToreRegulaer}", minuteWert: 90));
                _neunzig = true;
            }
            if (!_verlaengerungHalbzeit && _hatVerlaengerung && minute > 105)
            {
                Zeilen.Add(EinzelspielEreignisZeile.Trenner("HALBZEIT VERLÄNGERUNG", minuteWert: 105));
                _verlaengerungHalbzeit = true;
            }
            // Issue #232 Nachtrag: Schwelle statt fixer Nachbereitung, damit die (jetzt einzeln
            // aufgefuehrten) Elfmeterschiessen-Schuesse mit Minute > 120 chronologisch NACH diesem
            // Trenner in der Zeitleiste erscheinen.
            if (!_verlaengerungEnde && _hatVerlaengerung && minute > 120)
            {
                Zeilen.Add(EinzelspielEreignisZeile.Trenner($"ENDE VERLÄNGERUNG · {Endstand(true)}:{Endstand(false)}", minuteWert: 120));
                _verlaengerungEnde = true;
            }
        }

        public void Elfmetertrenner()
        {
            if (_hatElfmeter)
                Zeilen.Add(EinzelspielEreignisZeile.Trenner(
                    $"ELFMETERSCHIESSEN · {stand.HeimElfmetertore}:{stand.AuswaertsElfmetertore}", TrennerArt.Elfmeter));
        }

        public void Abpfiff()
        {
            string elfmeterZusatz = _hatElfmeter ? $" ({stand.HeimElfmetertore}:{stand.AuswaertsElfmetertore} i.E.)" : "";
            Zeilen.Add(EinzelspielEreignisZeile.Trenner(
                $"ABPFIFF · {Endstand(true)}:{Endstand(false)}{elfmeterZusatz}", TrennerArt.Ende));
        }

        // Stand nach 90 Minuten plus Verlängerung (ohne Elfmeterschießen und Wiederholungsspiel).
        private int Endstand(bool heimSeite) => heimSeite
            ? stand.HeimToreRegulaer + (stand.HeimToreVerlaengerung ?? 0)
            : stand.AuswaertsToreRegulaer + (stand.AuswaertsToreVerlaengerung ?? 0);

        private static string ArtText(EreignisArt art) => art switch
        {
            EreignisArt.Tor                 => "Tor",
            EreignisArt.GelbeKarte          => "Gelbe Karte",
            EreignisArt.RoteKarte           => "Rote Karte",
            EreignisArt.GoldenGoal          => "Golden Goal",
            EreignisArt.SilverGoal          => "Silver Goal",
            EreignisArt.ElfmeterTor         => "Elfmeter verwandelt",
            EreignisArt.ElfmeterVerschossen => "Elfmeter verschossen",
            // #431: vergebene Großchance oder vergebener Elfmeter im Spiel; der Detailtext nennt den Hergang.
            EreignisArt.Grosschance         => "Großchance",
            _                               => art.ToString()
        };

        // Issue #341: Beschreibung aus der Simulation (z. B. "Tor per Kopfball nach Konter (oben links)")
        // als sekundären Detailtext zeigen — ohne den Präfix, der schon als Art-Text dasteht; wiederholt
        // die Beschreibung nur den Art-Text (Karten, Elfmeter) oder fehlt sie (Altdaten), entfällt sie.
        private static string? DetailText(string artText, string beschreibung)
        {
            if (string.IsNullOrWhiteSpace(beschreibung)) return null;
            string rest = beschreibung.StartsWith(artText, StringComparison.OrdinalIgnoreCase)
                ? beschreibung[artText.Length..].Trim()
                : beschreibung.Trim();
            return rest.Length > 0 ? rest : null;
        }

        // Tore, die den laufenden Spielstand verändern (ein verwandelter Elfmeter im Spiel zählt hier nicht mit).
        private static bool VeraendertSpielstand(EreignisArt art) =>
            art is EreignisArt.Tor or EreignisArt.GoldenGoal or EreignisArt.SilverGoal;

        private EinzelspielEreignisZeile Zeile(ZeitleistenEreignis ereignis)
        {
            var seite = ereignis.IstHeimSeite ? heim : auswaerts;
            var art = ereignis.Art;
            string artText = ArtText(art);
            return new EinzelspielEreignisZeile(
                Spielminute.Text(ereignis.Minute, ereignis.Nachspielminute), seite.Name, artText, ereignis.IstHeimSeite,
                Wappen: seite.Wappen,
                IstTor: art is EreignisArt.Tor or EreignisArt.GoldenGoal or EreignisArt.SilverGoal or EreignisArt.ElfmeterTor,
                IstGelbeKarte: art == EreignisArt.GelbeKarte,
                IstRoteKarte: art == EreignisArt.RoteKarte,
                IstElfmeterVerschossen: art == EreignisArt.ElfmeterVerschossen,
                Detail: DetailText(artText, ereignis.Beschreibung),
                MinuteWert: ereignis.Minute,
                IstElfmeterschuss: art is EreignisArt.ElfmeterTor or EreignisArt.ElfmeterVerschossen,
                IstTaktikwechsel: art == EreignisArt.Taktikwechsel,
                IstGrosschance: art == EreignisArt.Grosschance);
        }
    }
}
