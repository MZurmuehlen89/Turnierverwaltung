using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Elfmeterschießen der <see cref="SimulationsEngine"/> (aus ihr herausgelöst, #503).</summary>
internal static class Elfmeterschiessen
{
    public static (int Heim, int Gast, IReadOnlyList<SimuliertesEreignis> Ereignisse) Simulieren(
        Mannschaft heim, Mannschaft auswaerts, Random rng, bool ausfuehrlich)
    {
        // Trefferwahrscheinlichkeit: Basis 73 % + leichte Anpassung über Offensivrating
        double pHeim = 0.73 + (heim.OffensivRating     - 70) * 0.002;
        double pGast = 0.73 + (auswaerts.OffensivRating - 70) * 0.002;
        pHeim = Math.Clamp(pHeim, 0.5, 0.92);
        pGast = Math.Clamp(pGast, 0.5, 0.92);

        int h = 0, g = 0;
        int minute = 120;
        var ereignisse = new List<SimuliertesEreignis>();

        // Minute läuft ab 121 hoch — rein zur Sortierung/Anzeige, es gibt im Elfmeterschießen
        // keine echte Spielminute. Geht immer erst nach vollständiger Verlängerung los (siehe
        // Kommentar in ZeitleisteHinzufuegen), daher keine Kollision mit Minuten aus der Verlängerung.
        void Schuss(bool istHeim, double trefferWahrscheinlichkeit)
        {
            minute++;
            bool tor = rng.NextDouble() < trefferWahrscheinlichkeit;
            if (istHeim) { if (tor) h++; } else { if (tor) g++; }
            if (ausfuehrlich)
                ereignisse.Add(new(
                    minute,
                    tor ? EreignisArt.ElfmeterTor : EreignisArt.ElfmeterVerschossen,
                    istHeim,
                    tor ? "Elfmeter verwandelt" : "Elfmeter verschossen"));
        }

        // Bricht ab, sobald der Torvorsprung mit den verbleibenden Versuchen des
        // zurückliegenden Teams rechnerisch nicht mehr aufholbar ist (Issue #235) —
        // Prüfung erfolgt nach jedem einzelnen Schuss, nicht erst nach jeder Runde.
        bool Entschieden(int heimSchuesse, int gastSchuesse)
        {
            int heimRest = 5 - heimSchuesse;
            int gastRest = 5 - gastSchuesse;
            return h - g > gastRest || g - h > heimRest;
        }

        for (int i = 0; i < 5; i++)
        {
            Schuss(true, pHeim);
            if (Entschieden(i + 1, i))
                break;

            Schuss(false, pGast);
            if (Entschieden(i + 1, i + 1))
                break;
        }

        // Plötzlicher Tod bei Gleichstand
        while (h == g)
        {
            Schuss(true, pHeim);
            Schuss(false, pGast);
        }

        return (h, g, ereignisse);
    }
}
