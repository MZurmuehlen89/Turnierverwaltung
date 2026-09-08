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
    bool IstTor = false, bool IstGelbeKarte = false, bool IstRoteKarte = false, bool IstElfmeterVerschossen = false)
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

    public static EinzelspielEreignisZeile Trenner(string text, TrennerArt art = TrennerArt.Abschnitt) =>
        new("", "", "", false, IstTrenner: true, TrennerText: text, TrennerArt: art);
}

/// <summary>Issue #237: eine Zeile der Ergebnis-Karte — entweder eine Team-vs-Team-Zeile mit Wappen/
/// Flaggen (<see cref="ZeigtTeams"/>) oder eine reine Text-Meldung (z. B. "Weiter: TeamX"), die
/// Score als Freitext nutzt.</summary>
public record ProtokollErgebnisZeile(
    string? Label,
    string? HeimName, byte[]? HeimWappen,
    string Score,
    string? AuswaertsName, byte[]? AuswaertsWappen,
    string Detail = "")
{
    public bool ZeigtTeams => HeimName is not null;

    // Issue #237: Kennzeichnung für die vereinheitlichte Spielbericht-Kopfzeile — die Hauptzeile
    // (Einzelspiel oder "Gesamt" bei Hin-/Rückspiel) wird groß als Scoreboard dargestellt, Hin-/
    // Rückspiel-Einzelergebnisse als kleine Chips, eine reine Text-Zeile (z. B. "Weiter: TeamX") als Pill.
    public bool IstHauptzeile => Label is null or "Gesamt";
    public bool IstBeinzeile  => Label is "Hinspiel" or "Rückspiel";
    public bool HatDetail     => !string.IsNullOrWhiteSpace(Detail);

    public static ProtokollErgebnisZeile Text(string? label, string text) =>
        new(label, null, null, text, null, null);
}

/// <summary>
/// Gemeinsame Protokoll-Formatierung für die Turnierspiel-Simulation (#302) — 1:1 aus
/// Wpf/ViewModels/SimulationsProtokollHelfer.cs portiert. Das WPF-Original teilt sich diese Klasse
/// zusätzlich mit der freien Einzelspiel-Simulation (#289 in dieser Issue-Serie, noch nicht portiert) —
/// hier vorerst nur vom Simulation-Tab genutzt.
/// </summary>
internal static class SimulationsProtokollHelfer
{
    public static (string Score, string Detail) ScoreUndDetail(SimulationsErgebnis e)
    {
        int heim = e.HeimToreRegulaer + (e.HeimToreVerlaengerung ?? 0);
        int gast = e.AuswaertsToreRegulaer + (e.AuswaertsToreVerlaengerung ?? 0);

        // Issue #237 (Nutzer-Feedback): bei Verlängerung/Elfmeterschießen weicht das kombinierte
        // Endergebnis vom 90-Minuten-Stand ab — ohne ihn zu nennen, ist z.B. bei "2:2 (i.E. 4:3)" nicht
        // erkennbar, wie das Spiel nach regulärer Spielzeit stand.
        bool hatteVerlaengerung = e.HeimToreVerlaengerung.HasValue && e.AuswaertsToreVerlaengerung.HasValue;
        string regulaerHinweis = hatteVerlaengerung ? $"90' {e.HeimToreRegulaer}:{e.AuswaertsToreRegulaer}, " : "";

        string detail = e.EntscheidungArt switch
        {
            EntscheidungArt.Verlaengerung     => $" ({regulaerHinweis}n.V.)",
            EntscheidungArt.GoldenGoal        => $" ({regulaerHinweis}G.G.)",
            EntscheidungArt.SilverGoal        => $" ({regulaerHinweis}S.G.)",
            EntscheidungArt.Elfmeterschiessen => $" ({regulaerHinweis}i.E. {e.HeimElfmetertore}:{e.AuswaertsElfmetertore})",
            EntscheidungArt.Auswaertstorregel => " (A.T.)",
            EntscheidungArt.Losentscheid      => " (Los)",
            EntscheidungArt.Muenzwurf         => " (Münze)",
            _                                 => ""
        };

        return ($"{heim}:{gast}", detail);
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
        IEnumerable<(int Minute, EreignisArt Art, bool IstHeimSeite)> ereignisse,
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
                ziel.Add(EinzelspielEreignisZeile.Trenner($"HALBZEIT · {heimStand}:{auswaertsStand}"));
                halbzeitEingefuegt = true;
            }
            if (!neunzigEingefuegt && (hatVerlaengerung || hatElfmeter) && minute > 90)
            {
                ziel.Add(EinzelspielEreignisZeile.Trenner($"90 MINUTEN · {heimToreRegulaer}:{auswaertsToreRegulaer}"));
                neunzigEingefuegt = true;
            }
            if (!vzHalbzeitEingefuegt && hatVerlaengerung && minute > 105)
            {
                ziel.Add(EinzelspielEreignisZeile.Trenner("HALBZEIT VERLÄNGERUNG"));
                vzHalbzeitEingefuegt = true;
            }
            // Issue #232 Nachtrag: Schwelle statt fixer Nachbereitung, damit die (jetzt einzeln
            // aufgefuehrten) Elfmeterschiessen-Schuesse mit Minute > 120 chronologisch NACH diesem
            // Trenner in der Zeitleiste erscheinen.
            if (!vzEndeEingefuegt && hatVerlaengerung && minute > 120)
            {
                int heimGesamt      = heimToreRegulaer      + (heimToreVerlaengerung ?? 0);
                int auswaertsGesamt = auswaertsToreRegulaer + (auswaertsToreVerlaengerung ?? 0);
                ziel.Add(EinzelspielEreignisZeile.Trenner($"ENDE VERLÄNGERUNG · {heimGesamt}:{auswaertsGesamt}"));
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

        if (anpfiffText is not null)
            ziel.Add(EinzelspielEreignisZeile.Trenner(anpfiffText, TrennerArt.Start));

        foreach (var (minute, art, istHeimSeite) in ereignisListe.OrderBy(ev => ev.Minute))
        {
            SchwellenPruefen(minute);

            if (art is EreignisArt.Tor or EreignisArt.GoldenGoal or EreignisArt.SilverGoal)
            {
                if (istHeimSeite) heimStand++; else auswaertsStand++;
            }

            ziel.Add(new EinzelspielEreignisZeile(
                $"{minute}'", istHeimSeite ? heimName : auswaertsName, ArtText(art), istHeimSeite,
                Wappen: istHeimSeite ? heimWappen : auswaertsWappen,
                IstTor: art is EreignisArt.Tor or EreignisArt.GoldenGoal or EreignisArt.SilverGoal or EreignisArt.ElfmeterTor,
                IstGelbeKarte: art == EreignisArt.GelbeKarte,
                IstRoteKarte: art == EreignisArt.RoteKarte,
                IstElfmeterVerschossen: art == EreignisArt.ElfmeterVerschossen));
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
