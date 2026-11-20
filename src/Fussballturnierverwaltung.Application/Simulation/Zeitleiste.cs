using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Issue #237: Trenner-Untertyp fürs Rendering der Zeitleisten-Grafik — Abschnitt/Elfmeter
/// durchbrechen die Mittelachse mit einer Linie, Start/Ende/Beinabschnitt stehen als eigenständige
/// Marke ohne Linie (Anpfiff/Abpfiff bzw. HINSPIEL/RÜCKSPIEL-Kopf bei Hin- und Rückspiel).</summary>
public enum TrennerArt { Abschnitt, Elfmeter, Start, Ende, Beinabschnitt, Wiederholung }

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
    public bool ZeigtAlsAbschnittMitLinie   => IstTrenner && TrennerArt is TrennerArt.Abschnitt or TrennerArt.Elfmeter or TrennerArt.Wiederholung;
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

/// <summary>Baut die Ereigniszeitleiste eines Spiels (#509, Schnitt A – aus <c>SimulationsProtokollHelfer</c> verschoben).</summary>
public static class Zeitleiste
{
    // Baut die Ereigniszeitleiste inkl. Abschnittstrenner (Halbzeit, 90 Minuten,
    // Halbzeit/Ende Verlängerung, Elfmeterschießen) für die übergebene Ziel-Collection auf.
    // Issue #237: rahmt die Zeitleiste zusätzlich mit ANPFIFF/ABPFIFF-Marken (Text per Parameter
    // überschreibbar, z. B. "ENDE HINSPIEL" bei Hin-/Rückspiel — null unterdrückt die jeweilige Marke,
    // etwa beim Rückspiel-Aufruf, wo der Aufrufer stattdessen selbst eine Gesamt-Abpfiff-Marke anhängt)
    // und trägt die Team-Wappen je Ereignis ein. Rückgabewert zeigt an, ob überhaupt Ereignisse
    // (und damit auch die Marken) hinzugefügt wurden.
    public static bool ZeitleisteHinzufuegen(
        ObservableCollection<EinzelspielEreignisZeile> ziel,
        IEnumerable<(int Minute, int? Nachspielminute, EreignisArt Art, bool IstHeimSeite, string Beschreibung, bool ImWiederholungsspiel)> ereignisse,
        string heimName, string auswaertsName,
        EntscheidungArt entscheidungArt,
        int heimToreRegulaer, int auswaertsToreRegulaer,
        int? heimToreVerlaengerung, int? auswaertsToreVerlaengerung,
        int? heimElfmetertore, int? auswaertsElfmetertore,
        byte[]? heimWappen = null, byte[]? auswaertsWappen = null,
        string? anpfiffText = "ANPFIFF", string? abpfiffPraefix = "ABPFIFF",
        Wiederholungsergebnis? wiederholung = null)
    {
        // Schnellsimulation persistiert keine Einzelereignisse (siehe SimulationsService.SpeichernAsync,
        // sim.Ereignisse.Any()-Gate) — ohne diesen fruehen Ausstieg wuerde SchwellenPruefen(int.MaxValue)
        // unten trotzdem einen "HALBZEIT"-Trenner einfuegen und eine leere Zeitleiste vortaeuschen.
        var alleEreignisse = ereignisse.ToList();
        if (alleEreignisse.Count == 0) return false;
        // #547: das Wiederholungsspiel hat einen eigenen Abschnitt mit eigener Uhr – seine Minuten zählen neu.
        var ereignisListe = alleEreignisse.Where(ev => !ev.ImWiederholungsspiel).ToList();
        var wiederholungsEreignisse = alleEreignisse.Where(ev => ev.ImWiederholungsspiel).ToList();

        // Verlaengerung fand statt, sobald die Verlaengerungs-Torfelder gesetzt sind — unabhaengig
        // von der finalen EntscheidungArt (auch bei Elfmeterschiessen ging dem immer eine
        // vollstaendige Verlaengerung voraus, siehe ErgebnisEntscheidung.Art).
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
            // #431: vergebene Großchance oder vergebener Elfmeter im Spiel; der Detailtext nennt den Hergang.
            EreignisArt.Grosschance         => "Großchance",
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

        // #361: Nachspielzeit („90+4'“) nach der regulären Minute, Ereignisse derselben Minute in Entstehungsreihenfolge.
        EinzelspielEreignisZeile Zeile(int minute, int? nachspielminute, EreignisArt art, bool istHeimSeite, string beschreibung)
        {
            string artText = ArtText(art);
            return new EinzelspielEreignisZeile(
                Spielminute.Text(minute, nachspielminute), istHeimSeite ? heimName : auswaertsName, artText, istHeimSeite,
                Wappen: istHeimSeite ? heimWappen : auswaertsWappen,
                IstTor: art is EreignisArt.Tor or EreignisArt.GoldenGoal or EreignisArt.SilverGoal or EreignisArt.ElfmeterTor,
                IstGelbeKarte: art == EreignisArt.GelbeKarte,
                IstRoteKarte: art == EreignisArt.RoteKarte,
                IstElfmeterVerschossen: art == EreignisArt.ElfmeterVerschossen,
                Detail: DetailText(artText, beschreibung),
                MinuteWert: minute,
                IstElfmeterschuss: art is EreignisArt.ElfmeterTor or EreignisArt.ElfmeterVerschossen,
                IstTaktikwechsel: art == EreignisArt.Taktikwechsel,
                IstGrosschance: art == EreignisArt.Grosschance);
        }

        foreach (var (minute, nachspielminute, art, istHeimSeite, beschreibung, _) in
                 ereignisListe.Chronologisch(ev => ev.Minute, ev => ev.Nachspielminute))
        {
            SchwellenPruefen(minute);

            if (art is EreignisArt.Tor or EreignisArt.GoldenGoal or EreignisArt.SilverGoal)
            {
                if (istHeimSeite) heimStand++; else auswaertsStand++;
            }

            ziel.Add(Zeile(minute, nachspielminute, art, istHeimSeite, beschreibung));
        }

        // Noch nicht erreichte Schwellen (z. B. Halbzeit ohne Tor, Spielende ohne Ereignis danach) nachtragen
        SchwellenPruefen(int.MaxValue);

        if (wiederholung is not null || wiederholungsEreignisse.Count > 0)
        {
            ziel.Add(EinzelspielEreignisZeile.Trenner("WIEDERHOLUNGSSPIEL", TrennerArt.Wiederholung));
            foreach (var (minute, nachspielminute, art, istHeimSeite, beschreibung, _) in
                     wiederholungsEreignisse.Chronologisch(ev => ev.Minute, ev => ev.Nachspielminute))
                ziel.Add(Zeile(minute, nachspielminute, art, istHeimSeite, beschreibung));

            if (wiederholung is { } wdh)
                ziel.Add(EinzelspielEreignisZeile.Trenner(
                    $"ENDE WIEDERHOLUNGSSPIEL · {wdh.HeimGesamt}:{wdh.AuswaertsGesamt}", TrennerArt.Wiederholung));
        }

        if (hatElfmeter)
            ziel.Add(EinzelspielEreignisZeile.Trenner(
                $"ELFMETERSCHIESSEN · {heimElfmetertore}:{auswaertsElfmetertore}", TrennerArt.Elfmeter));

        if (abpfiffPraefix is not null)
        {
            int heimEndstand      = heimToreRegulaer      + (heimToreVerlaengerung ?? 0);
            int auswaertsEndstand = auswaertsToreRegulaer + (auswaertsToreVerlaengerung ?? 0);
            string elfmeterZusatz = hatElfmeter ? $" ({heimElfmetertore}:{auswaertsElfmetertore} i.E.)" : "";
            if (wiederholung is { } wiederholungsStand)
                elfmeterZusatz += $" (Wdh. {wiederholungsStand.HeimGesamt}:{wiederholungsStand.AuswaertsGesamt})";
            ziel.Add(EinzelspielEreignisZeile.Trenner(
                $"{abpfiffPraefix} · {heimEndstand}:{auswaertsEndstand}{elfmeterZusatz}", TrennerArt.Ende));
        }

        return true;
    }
}
