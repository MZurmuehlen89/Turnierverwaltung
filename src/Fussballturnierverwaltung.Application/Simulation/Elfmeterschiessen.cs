using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Elfmeterschießen der <see cref="SimulationsEngine"/>: fünf Schüsse je Seite mit vorzeitigem Abbruch,
/// danach Sudden Death. Ein Exemplar hält den Zwischenstand eines einzelnen Elfmeterschießens (aus der Engine
/// herausgelöst, #503).</summary>
internal sealed class Elfmeterschiessen
{
    private const int REGULAERE_SCHUESSE = 5;

    private readonly Random _rng;
    private readonly bool _ausfuehrlich;
    private readonly double _trefferHeim;
    private readonly double _trefferGast;
    private readonly List<SimuliertesEreignis> _ereignisse = [];
    private int _heim;
    private int _gast;

    // Minute läuft ab 121 hoch — rein zur Sortierung/Anzeige, es gibt im Elfmeterschießen
    // keine echte Spielminute. Geht immer erst nach vollständiger Verlängerung los (siehe
    // Kommentar in ZeitleisteHinzufuegen), daher keine Kollision mit Minuten aus der Verlängerung.
    private int _minute = 120;

    private Elfmeterschiessen(Mannschaft heim, Mannschaft auswaerts, Random rng, bool ausfuehrlich)
    {
        _rng          = rng;
        _ausfuehrlich = ausfuehrlich;
        _trefferHeim  = Trefferwahrscheinlichkeit(heim);
        _trefferGast  = Trefferwahrscheinlichkeit(auswaerts);
    }

    public static (int Heim, int Gast, IReadOnlyList<SimuliertesEreignis> Ereignisse) Simulieren(
        Mannschaft heim, Mannschaft auswaerts, Random rng, bool ausfuehrlich)
    {
        var elfmeterschiessen = new Elfmeterschiessen(heim, auswaerts, rng, ausfuehrlich);
        elfmeterschiessen.RegulaereSchuesse();
        elfmeterschiessen.SuddenDeath();
        return (elfmeterschiessen._heim, elfmeterschiessen._gast, elfmeterschiessen._ereignisse);
    }

    /// <summary>Basis 73 % plus leichte Anpassung über das Offensivrating.</summary>
    private static double Trefferwahrscheinlichkeit(Mannschaft schuetzen)
        => Math.Clamp(0.73 + (schuetzen.OffensivRating - 70) * 0.002, 0.5, 0.92);

    private void RegulaereSchuesse()
    {
        for (int i = 0; i < REGULAERE_SCHUESSE; i++)
        {
            Schuss(istHeim: true);
            if (Entschieden(i + 1, i))
                return;

            Schuss(istHeim: false);
            if (Entschieden(i + 1, i + 1))
                return;
        }
    }

    private void SuddenDeath()
    {
        while (_heim == _gast)
        {
            Schuss(istHeim: true);
            Schuss(istHeim: false);
        }
    }

    private void Schuss(bool istHeim)
    {
        _minute++;
        bool tor = _rng.NextDouble() < (istHeim ? _trefferHeim : _trefferGast);
        if (tor)
        {
            if (istHeim) _heim++;
            else _gast++;
        }
        if (_ausfuehrlich)
            _ereignisse.Add(new(
                _minute,
                tor ? EreignisArt.ElfmeterTor : EreignisArt.ElfmeterVerschossen,
                istHeim,
                tor ? "Elfmeter verwandelt" : "Elfmeter verschossen"));
    }

    /// <summary>Entschieden, sobald der Torvorsprung mit den verbleibenden Versuchen des zurückliegenden Teams
    /// rechnerisch nicht mehr aufholbar ist (Issue #235) — geprüft nach jedem einzelnen Schuss, nicht erst nach
    /// jeder Runde.</summary>
    private bool Entschieden(int heimSchuesse, int gastSchuesse)
    {
        int heimRest = REGULAERE_SCHUESSE - heimSchuesse;
        int gastRest = REGULAERE_SCHUESSE - gastSchuesse;
        return _heim - _gast > gastRest || _gast - _heim > heimRest;
    }
}
