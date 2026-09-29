using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Issue #237: Trenner-Untertyp fürs Rendering der Zeitleisten-Grafik — Abschnitt/Elfmeter
/// durchbrechen die Mittelachse mit einer Linie, Start/Ende/Beinabschnitt stehen als eigenständige
/// Marke ohne Linie (Anpfiff/Abpfiff bzw. HINSPIEL/RÜCKSPIEL-Kopf bei Hin- und Rückspiel).</summary>
public enum TrennerArt { Abschnitt, Elfmeter, Start, Ende, Beinabschnitt }

public record EinzelspielEreignisZeile(
    string Minute, string Seite, string Art, bool IstHeimSeite,
    bool IstTrenner = false, string? TrennerText = null, TrennerArt TrennerArt = TrennerArt.Abschnitt,
    byte[]? Wappen = null,
    bool IstTor = false, bool IstGelbeKarte = false, bool IstRoteKarte = false, bool IstElfmeterVerschossen = false,
    string? Detail = null, int MinuteWert = 0, bool IstElfmeterschuss = false)
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


/// <summary>Issue #341: Spielkopf nach kicker.de-Vorbild — Kontextzeile (Turnier/Runde bzw. Hinspiel/
/// Rückspiel), große Wappen mit Name und Untertitel, Ergebnis-Box mit Entscheidungsart und
/// Zwischenständen. Gemeinsam genutzt von Spielbericht-Karte und Hinspiel-/Rückspiel-Karten
/// (<see cref="Views.SpielKopfControl"/>).</summary>
public sealed record SpielKopf(
    string? Kontext,
    string HeimName, byte[]? HeimWappen, string? HeimUntertitel, string HeimKuerzel,
    string AuswaertsName, byte[]? AuswaertsWappen, string? AuswaertsUntertitel, string AuswaertsKuerzel,
    string Score, string? Entscheidung, string? Zwischenstaende)
{
    public bool HatKontext             => Kontext is not null;
    public bool HatHeimUntertitel      => HeimUntertitel is not null;
    public bool HatAuswaertsUntertitel => AuswaertsUntertitel is not null;
    public bool HatEntscheidung        => Entscheidung is not null;
    public bool HatZwischenstaende     => Zwischenstaende is not null;
}

/// <summary>Issue #341: Kopf einer Hinspiel-/Rückspiel-Karte (Expander-Header) — Spielkopf plus
/// kompakter Zeitstrahl in einem Objekt, damit der Header über den bewährten Header/HeaderTemplate-Weg
/// gebunden bleibt (jede neue Simulation liefert eine neue Instanz).</summary>
public sealed record SpielBeinAnsicht(SpielKopf Kopf, IReadOnlyCollection<EinzelspielEreignisZeile> Ereignisse)
{
    public bool HatEreignisse => Ereignisse.Count > 0;
}

/// <summary>
/// Gemeinsame Protokoll-Formatierung für die Turnierspiel-Simulation (#302) — 1:1 aus
/// Wpf/ViewModels/SimulationsProtokollHelfer.cs portiert. Das WPF-Original teilt sich diese Klasse
/// zusätzlich mit der freien Einzelspiel-Simulation (#289 in dieser Issue-Serie, noch nicht portiert) —
/// hier vorerst nur vom Simulation-Tab genutzt.
/// </summary>
internal static class SimulationsProtokollHelfer
{
    // ── Spielkopf (Issue #341) ────────────────────────────────────────────────

    /// <summary>Baut den Spielkopf aus einem Ergebnis. <paramref name="verbandKurzname"/> liefert den
    /// Kontinentalverband einer Mannschaft (die Aufrufer laden Verbände unterschiedlich).</summary>
    public static SpielKopf SpielKopfErstellen(
        string? kontext,
        Mannschaft? heim, string heimName,
        Mannschaft? auswaerts, string auswaertsName,
        SimulationsErgebnis e,
        Func<Mannschaft, string?> verbandKurzname)
    {
        // Wie bei kicker: nach Elfmeterschießen steht der Elfmeterstand groß in der Box, das Ergebnis
        // nach Verlängerung wandert in die Zwischenstände.
        bool imElfmeterschiessen = e is { EntscheidungArt: EntscheidungArt.Elfmeterschiessen, HeimElfmetertore: not null, AuswaertsElfmetertore: not null };
        string score = imElfmeterschiessen
            ? $"{e.HeimElfmetertore}:{e.AuswaertsElfmetertore}"
            : $"{e.HeimToreRegulaer + (e.HeimToreVerlaengerung ?? 0)}:{e.AuswaertsToreRegulaer + (e.AuswaertsToreVerlaengerung ?? 0)}";

        string? entscheidung = e.EntscheidungArt switch
        {
            EntscheidungArt.Elfmeterschiessen => "im Elfmeterschießen",
            EntscheidungArt.Verlaengerung     => "nach Verlängerung",
            EntscheidungArt.GoldenGoal        => "nach Golden Goal",
            EntscheidungArt.SilverGoal        => "nach Silver Goal",
            EntscheidungArt.Auswaertstorregel => "nach Auswärtstorregel",
            EntscheidungArt.Losentscheid      => "per Losentscheid",
            EntscheidungArt.Muenzwurf         => "per Münzwurf",
            _                                 => null
        };

        return new SpielKopf(
            kontext,
            heimName, MannschaftsIcon(heim), Untertitel(heim, auswaerts, verbandKurzname), Kuerzel(heim, heimName),
            auswaertsName, MannschaftsIcon(auswaerts), Untertitel(auswaerts, heim, verbandKurzname), Kuerzel(auswaerts, auswaertsName),
            score, entscheidung, ZwischenstandText(e));
    }

    /// <summary>Gesamtkopf einer Hin-/Rückspiel-Paarung aus Sicht des Hinspiel-Heimteams — Summenergebnis
    /// in der Box, <paramref name="zusatz"/> ("Ergebnis nach Hinspiel", "nach 180 Minuten", …) darunter;
    /// ohne Zwischenstands-Streifen, die Einzelergebnisse stehen in den Karten darüber.</summary>
    public static SpielKopf GesamtKopfErstellen(
        Mannschaft heim, Mannschaft auswaerts, string score, string zusatz,
        Func<Mannschaft, string?> verbandKurzname) =>
        new("Gesamt",
            heim.Name, MannschaftsIcon(heim), Untertitel(heim, auswaerts, verbandKurzname), Kuerzel(heim, heim.Name),
            auswaerts.Name, MannschaftsIcon(auswaerts), Untertitel(auswaerts, heim, verbandKurzname), Kuerzel(auswaerts, auswaerts.Name),
            score, zusatz, null);

    // Zwischenstände ("HZ 1:0 · 90' 1:1 · n.V. 2:1"). Der Halbzeitstand fehlt bei manuell eingegebenen
    // Ergebnissen — ohne jeden Zwischenstand liefert die Methode null.
    private static string? ZwischenstandText(SimulationsErgebnis e)
    {
        var teile = new List<string>();
        if (e is { HalbzeitHeimTore: int hzHeim, HalbzeitAuswaertsTore: int hzGast })
            teile.Add($"HZ {hzHeim}:{hzGast}");
        bool hatVerlaengerung = e is { HeimToreVerlaengerung: not null, AuswaertsToreVerlaengerung: not null };
        if (hatVerlaengerung || e.EntscheidungArt == EntscheidungArt.Elfmeterschiessen)
            teile.Add($"90' {e.HeimToreRegulaer}:{e.AuswaertsToreRegulaer}");
        if (hatVerlaengerung)
            teile.Add($"n.V. {e.HeimToreRegulaer + e.HeimToreVerlaengerung}:{e.AuswaertsToreRegulaer + e.AuswaertsToreVerlaengerung}");
        return teile.Count > 0 ? string.Join(" · ", teile) : null;
    }

    // Untertitel je nach Kontext der Begegnung (Nutzer-Vorgabe #341): Nationalmannschaft → Kontinental-
    // verband; Verein → Land, außer beide sind Vereine desselben Landes (dann nichtssagend → keiner;
    // dort gehört künftig die Liga hin, siehe #384).
    private static string? Untertitel(Mannschaft? mannschaft, Mannschaft? gegner, Func<Mannschaft, string?> verbandKurzname)
    {
        if (mannschaft is null) return null;
        if (mannschaft.Mannschaftsart == Mannschaftsart.Nationalmannschaft)
            return verbandKurzname(mannschaft);
        bool nationalesVereinsduell = gegner is { Mannschaftsart: Mannschaftsart.Verein } && gegner.LandId == mannschaft.LandId;
        return nationalesVereinsduell ? null : mannschaft.Land?.Name;
    }

    // Issue #341: Team-Kürzel am Zeitstrahl — Kurzname, sonst die ersten drei Buchstaben des Namens
    // (auch für Platzhalter-Teams ohne Mannschaft, z. B. "Sieger Gruppe A").
    public static string Kuerzel(Mannschaft? mannschaft, string ersatzName)
    {
        if (!string.IsNullOrWhiteSpace(mannschaft?.Kurzname)) return mannschaft.Kurzname;
        string name = mannschaft?.Name ?? ersatzName;
        return name[..Math.Min(3, name.Length)].ToUpperInvariant();
    }

    // Issue #232, analog #229/#230: Nationalmannschaften zeigen die Landesflagge statt des
    // Mannschafts-Wappens, das eigene Wappen bleibt in der DB erhalten.
    public static byte[]? MannschaftsIcon(Mannschaft? mannschaft)
        => mannschaft?.Mannschaftsart == Mannschaftsart.Nationalmannschaft
            ? mannschaft.Land?.Flagge
            : mannschaft?.Wappen;

    // Baut die Ereigniszeitleiste inkl. Abschnittstrenner (Halbzeit, 90 Minuten,
    // Halbzeit/Ende Verlängerung, Elfmeterschießen) für die übergebene Ziel-Collection auf.
    // Issue #237: rahmt die Zeitleiste zusätzlich mit ANPFIFF/ABPFIFF-Marken (Text per Parameter
    // überschreibbar, z. B. "ENDE HINSPIEL" bei Hin-/Rückspiel — null unterdrückt die jeweilige Marke,
    // etwa beim Rückspiel-Aufruf, wo der Aufrufer stattdessen selbst eine Gesamt-Abpfiff-Marke anhängt)
    // und trägt die Team-Wappen je Ereignis ein. Rückgabewert zeigt an, ob überhaupt Ereignisse
    // (und damit auch die Marken) hinzugefügt wurden.
    public static bool ZeitleisteHinzufuegen(
        ObservableCollection<EinzelspielEreignisZeile> ziel,
        IEnumerable<(int Minute, EreignisArt Art, bool IstHeimSeite, string Beschreibung)> ereignisse,
        string heimName, string auswaertsName,
        EntscheidungArt entscheidungArt,
        int heimToreRegulaer, int auswaertsToreRegulaer,
        int? heimToreVerlaengerung, int? auswaertsToreVerlaengerung,
        int? heimElfmetertore, int? auswaertsElfmetertore,
        byte[]? heimWappen = null, byte[]? auswaertsWappen = null,
        string? anpfiffText = "ANPFIFF", string? abpfiffPraefix = "ABPFIFF")
    {
        // Schnellsimulation persistiert keine Einzelereignisse (siehe SimulationsService.SpeichernAsync,
        // sim.Ereignisse.Any()-Gate) — ohne diesen fruehen Ausstieg wuerde SchwellenPruefen(int.MaxValue)
        // unten trotzdem einen "HALBZEIT"-Trenner einfuegen und eine leere Zeitleiste vortaeuschen.
        var ereignisListe = ereignisse.ToList();
        if (ereignisListe.Count == 0) return false;

        // Verlaengerung fand statt, sobald die Verlaengerungs-Torfelder gesetzt sind — unabhaengig
        // von der finalen EntscheidungArt (auch bei Elfmeterschiessen ging dem immer eine
        // vollstaendige Verlaengerung voraus, siehe BerechneEntscheidungArt in SpielplanViewModel).
        bool hatVerlaengerung = heimToreVerlaengerung.HasValue && auswaertsToreVerlaengerung.HasValue;
        bool hatElfmeter      = entscheidungArt == EntscheidungArt.Elfmeterschiessen;

        bool halbzeitEingefuegt = false, neunzigEingefuegt = false, vzHalbzeitEingefuegt = false, vzEndeEingefuegt = false;
        int heimStand = 0, auswaertsStand = 0;

        void SchwellenPruefen(int minute)
        {
            if (!halbzeitEingefuegt && minute > 45)
            {
                ziel.Add(EinzelspielEreignisZeile.Trenner($"HALBZEIT · {heimStand}:{auswaertsStand}", minuteWert: 45));
                halbzeitEingefuegt = true;
            }
            if (!neunzigEingefuegt && (hatVerlaengerung || hatElfmeter) && minute > 90)
            {
                ziel.Add(EinzelspielEreignisZeile.Trenner($"90 MINUTEN · {heimToreRegulaer}:{auswaertsToreRegulaer}", minuteWert: 90));
                neunzigEingefuegt = true;
            }
            if (!vzHalbzeitEingefuegt && hatVerlaengerung && minute > 105)
            {
                ziel.Add(EinzelspielEreignisZeile.Trenner("HALBZEIT VERLÄNGERUNG", minuteWert: 105));
                vzHalbzeitEingefuegt = true;
            }
            // Issue #232 Nachtrag: Schwelle statt fixer Nachbereitung, damit die (jetzt einzeln
            // aufgefuehrten) Elfmeterschiessen-Schuesse mit Minute > 120 chronologisch NACH diesem
            // Trenner in der Zeitleiste erscheinen.
            if (!vzEndeEingefuegt && hatVerlaengerung && minute > 120)
            {
                int heimGesamt      = heimToreRegulaer      + (heimToreVerlaengerung ?? 0);
                int auswaertsGesamt = auswaertsToreRegulaer + (auswaertsToreVerlaengerung ?? 0);
                ziel.Add(EinzelspielEreignisZeile.Trenner($"ENDE VERLÄNGERUNG · {heimGesamt}:{auswaertsGesamt}", minuteWert: 120));
                vzEndeEingefuegt = true;
            }
        }

        static string ArtText(EreignisArt art) => art switch
        {
            EreignisArt.Tor                 => "Tor",
            EreignisArt.GelbeKarte          => "Gelbe Karte",
            EreignisArt.RoteKarte           => "Rote Karte",
            EreignisArt.GoldenGoal          => "Golden Goal",
            EreignisArt.SilverGoal          => "Silver Goal",
            EreignisArt.ElfmeterTor         => "Elfmeter verwandelt",
            EreignisArt.ElfmeterVerschossen => "Elfmeter verschossen",
            _                               => art.ToString()
        };

        // Issue #341: Beschreibung aus der Simulation (z. B. "Tor per Kopfball nach Konter (oben links)")
        // als sekundären Detailtext zeigen — ohne den Präfix, der schon als Art-Text dasteht; wiederholt
        // die Beschreibung nur den Art-Text (Karten, Elfmeter) oder fehlt sie (Altdaten), entfällt sie.
        static string? DetailText(string artText, string beschreibung)
        {
            if (string.IsNullOrWhiteSpace(beschreibung)) return null;
            string rest = beschreibung.StartsWith(artText, StringComparison.OrdinalIgnoreCase)
                ? beschreibung[artText.Length..].Trim()
                : beschreibung.Trim();
            return rest.Length > 0 ? rest : null;
        }

        if (anpfiffText is not null)
            ziel.Add(EinzelspielEreignisZeile.Trenner(anpfiffText, TrennerArt.Start));

        foreach (var (minute, art, istHeimSeite, beschreibung) in ereignisListe.OrderBy(ev => ev.Minute))
        {
            SchwellenPruefen(minute);

            if (art is EreignisArt.Tor or EreignisArt.GoldenGoal or EreignisArt.SilverGoal)
            {
                if (istHeimSeite) heimStand++; else auswaertsStand++;
            }

            string artText = ArtText(art);
            ziel.Add(new EinzelspielEreignisZeile(
                $"{minute}'", istHeimSeite ? heimName : auswaertsName, artText, istHeimSeite,
                Wappen: istHeimSeite ? heimWappen : auswaertsWappen,
                IstTor: art is EreignisArt.Tor or EreignisArt.GoldenGoal or EreignisArt.SilverGoal or EreignisArt.ElfmeterTor,
                IstGelbeKarte: art == EreignisArt.GelbeKarte,
                IstRoteKarte: art == EreignisArt.RoteKarte,
                IstElfmeterVerschossen: art == EreignisArt.ElfmeterVerschossen,
                Detail: DetailText(artText, beschreibung),
                MinuteWert: minute,
                IstElfmeterschuss: art is EreignisArt.ElfmeterTor or EreignisArt.ElfmeterVerschossen));
        }

        // Noch nicht erreichte Schwellen (z. B. Halbzeit ohne Tor, Spielende ohne Ereignis danach) nachtragen
        SchwellenPruefen(int.MaxValue);

        if (hatElfmeter)
            ziel.Add(EinzelspielEreignisZeile.Trenner(
                $"ELFMETERSCHIESSEN · {heimElfmetertore}:{auswaertsElfmetertore}", TrennerArt.Elfmeter));

        if (abpfiffPraefix is not null)
        {
            int heimEndstand      = heimToreRegulaer      + (heimToreVerlaengerung ?? 0);
            int auswaertsEndstand = auswaertsToreRegulaer + (auswaertsToreVerlaengerung ?? 0);
            string elfmeterZusatz = hatElfmeter ? $" ({heimElfmetertore}:{auswaertsElfmetertore} i.E.)" : "";
            ziel.Add(EinzelspielEreignisZeile.Trenner(
                $"{abpfiffPraefix} · {heimEndstand}:{auswaertsEndstand}{elfmeterZusatz}", TrennerArt.Ende));
        }

        return true;
    }
}
