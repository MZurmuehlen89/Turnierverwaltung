using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.Wpf.ViewModels;

/// <summary>Issue #232: eine Zeile der Ergebnis-Karte — entweder eine Team-vs-Team-Zeile mit Wappen/
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

    public static ProtokollErgebnisZeile Text(string? label, string text) =>
        new(label, null, null, text, null, null);
}

/// <summary>
/// Gemeinsame Protokoll-Formatierung für Turnierspiel- und Einzelspiel-Simulation (Issue #164/#174) —
/// aus SimulationsViewModel extrahiert, damit beide ViewModels dieselbe Logik nutzen statt sie
/// zu duplizieren.
/// </summary>
internal static class SimulationsProtokollHelfer
{
    public static (string Score, string Detail) ScoreUndDetail(SimulationsErgebnis e)
    {
        int heim = e.HeimToreRegulaer + (e.HeimToreVerlaengerung ?? 0);
        int gast = e.AuswaertsToreRegulaer + (e.AuswaertsToreVerlaengerung ?? 0);

        string detail = e.EntscheidungArt switch
        {
            EntscheidungArt.Verlaengerung     => " (n.V.)",
            EntscheidungArt.GoldenGoal        => " (G.G.)",
            EntscheidungArt.SilverGoal        => " (S.G.)",
            EntscheidungArt.Elfmeterschiessen => $" (i.E. {e.HeimElfmetertore}:{e.AuswaertsElfmetertore})",
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
    public static void ZeitleisteHinzufuegen(
        ObservableCollection<EinzelspielEreignisZeile> ziel,
        IEnumerable<(int Minute, EreignisArt Art, bool IstHeimSeite)> ereignisse,
        string heimName, string auswaertsName,
        EntscheidungArt entscheidungArt,
        int heimToreRegulaer, int auswaertsToreRegulaer,
        int? heimToreVerlaengerung, int? auswaertsToreVerlaengerung,
        int? heimElfmetertore, int? auswaertsElfmetertore,
        string? minutenPraefix = null)
    {
        // Schnellsimulation persistiert keine Einzelereignisse (siehe SimulationsService.SpeichernAsync,
        // sim.Ereignisse.Any()-Gate) — ohne diesen fruehen Ausstieg wuerde SchwellenPruefen(int.MaxValue)
        // unten trotzdem einen "HALBZEIT"-Trenner einfuegen und eine leere Zeitleiste vortaeuschen.
        var ereignisListe = ereignisse.ToList();
        if (ereignisListe.Count == 0) return;

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
                ziel.Add(EinzelspielEreignisZeile.Trenner($"HALBZEIT ({heimStand}:{auswaertsStand})"));
                halbzeitEingefuegt = true;
            }
            if (!neunzigEingefuegt && (hatVerlaengerung || hatElfmeter) && minute > 90)
            {
                ziel.Add(EinzelspielEreignisZeile.Trenner($"90 MINUTEN ({heimToreRegulaer}:{auswaertsToreRegulaer})"));
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
                ziel.Add(EinzelspielEreignisZeile.Trenner($"ENDE VERLÄNGERUNG ({heimGesamt}:{auswaertsGesamt})"));
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

        foreach (var (minute, art, istHeimSeite) in ereignisListe.OrderBy(ev => ev.Minute))
        {
            SchwellenPruefen(minute);

            if (art is EreignisArt.Tor or EreignisArt.GoldenGoal or EreignisArt.SilverGoal)
            {
                if (istHeimSeite) heimStand++; else auswaertsStand++;
            }

            string minuteText = minutenPraefix is null ? $"{minute}'" : $"{minutenPraefix} {minute}'";
            ziel.Add(new EinzelspielEreignisZeile(
                minuteText, istHeimSeite ? heimName : auswaertsName, ArtText(art), istHeimSeite));
        }

        // Noch nicht erreichte Schwellen (z. B. Halbzeit ohne Tor, Spielende ohne Ereignis danach) nachtragen
        SchwellenPruefen(int.MaxValue);

        if (hatElfmeter)
            ziel.Add(EinzelspielEreignisZeile.Trenner($"ELFMETERSCHIESSEN ({heimElfmetertore}:{auswaertsElfmetertore})"));
    }
}
