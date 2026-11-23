using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Ermittelt den <see cref="Tabellenbedarf"/> eines Teams aus dem Stand vor dem Spieltag (#176): rein und ohne Zufall. Gerechnet
/// wird wie in <see cref="RangPrognose"/> über Punkte-Schranken – bester Rang nach lauter Siegen gegen lauter Niederlagen der
/// anderen, schlechtester umgekehrt, Punktgleichheit zählt als „kann vorbeiziehen“ –, ohne Tiebreaker. Die Zonen sind die der
/// Runde (<see cref="TabellenZonen"/>); eine Zone unter Vorbehalt („die n besten Dritten“) gilt nie als sicher.
/// </summary>
public static class TabellenbedarfRechner
{
    private const int ABSTIEGSKAMPF_MAX_OFFEN = 10; // höchstens so viele offene Spiele
    private const int ABSTIEGSKAMPF_LUFT      = 3;  // Punkte über dem ersten Abstiegsplatz

    public static Tabellenbedarf Berechnen(
        int mannschaftId, IReadOnlyList<Tabellenstand> tabelle, IReadOnlyList<TabellenZone> zonen,
        int? gruppenNummer, PunkteRegel punkte)
    {
        var eigen = tabelle.FirstOrDefault(s => s.MannschaftId == mannschaftId);
        if (zonen.Count == 0 || eigen.MannschaftId != mannschaftId || eigen.Offen == 0) return Tabellenbedarf.Offen;

        var lage = new Lage(tabelle, eigen, Zonenstufen.Aus(zonen, gruppenNummer, tabelle.Count), punkte);
        int bester = lage.BesterRang(lage.MaxPunkte(eigen.Punkte, eigen.Offen));
        int schlechtester = lage.SchlechtesterRang(lage.MinPunkte(eigen.Punkte, eigen.Offen));
        var stufen = lage.Stufen;

        if (!stufen.HatZone) return Tabellenbedarf.Offen; // keine Zone der Runde gilt für diese Gruppe

        if (stufen.Stufe(bester) == stufen.Stufe(schlechtester) && !stufen.UnterVorbehalt(bester)) return Tabellenbedarf.OhneZiel;

        int ziel = stufen.Stufe(bester);
        if (!stufen.UnterVorbehalt(bester) && RemisSichert(lage, ziel)) return Tabellenbedarf.RemisReicht;
        if (SiegNoetig(lage, ziel)) return Tabellenbedarf.SiegNoetig;
        return Abstiegskampf(lage, schlechtester) ? Tabellenbedarf.Abstiegskampf : Tabellenbedarf.Offen;
    }

    /// <summary>Mit einem Remis im anstehenden Spiel steht auch im schlechtesten Fall noch die Zielzone fest.</summary>
    private static bool RemisSichert(Lage lage, int ziel)
    {
        int nachRemis = lage.Eigen.Punkte + lage.Punkte.PunkteFuerUnentschieden;
        int schlechtesterNachRemis = lage.SchlechtesterRang(lage.MinPunkte(nachRemis, lage.Eigen.Offen - 1));
        return lage.Stufen.Stufe(schlechtesterNachRemis) == ziel;
    }

    /// <summary>Nach einem Remis ist die Zielzone auch bei lauter Siegen nicht mehr erreichbar.</summary>
    private static bool SiegNoetig(Lage lage, int ziel)
    {
        int nachRemis = lage.Eigen.Punkte + lage.Punkte.PunkteFuerUnentschieden;
        int besterNachRemis = lage.BesterRang(lage.MaxPunkte(nachRemis, lage.Eigen.Offen - 1));
        return lage.Stufen.Stufe(besterNachRemis) > ziel;
    }

    private static bool Abstiegskampf(Lage lage, int schlechtesterRang)
    {
        if (lage.Eigen.Offen > ABSTIEGSKAMPF_MAX_OFFEN) return false;
        if (lage.Stufen.ErsterAbstiegsplatz is not { } erster || erster > lage.Tabelle.Count) return false;
        if (schlechtesterRang < erster) return false; // Klassenerhalt gesichert

        int punkteAmErstenAbstiegsplatz = lage.Tabelle.Select(s => s.Punkte).OrderDescending().ElementAt(erster - 1);
        return lage.Eigen.Punkte - punkteAmErstenAbstiegsplatz <= ABSTIEGSKAMPF_LUFT;
    }

    // ── Punkte-Schranken ──────────────────────────────────────────────────

    private sealed record Lage(
        IReadOnlyList<Tabellenstand> Tabelle, Tabellenstand Eigen, Zonenstufen Stufen, PunkteRegel Punkte)
    {
        private int MinJeSpiel => Math.Min(Punkte.PunkteFuerSieg, Math.Min(Punkte.PunkteFuerUnentschieden, Punkte.PunkteFuerNiederlage));
        private int MaxJeSpiel => Math.Max(Punkte.PunkteFuerSieg, Math.Max(Punkte.PunkteFuerUnentschieden, Punkte.PunkteFuerNiederlage));

        public int MinPunkte(int punkte, int offen) => punkte + offen * MinJeSpiel;
        public int MaxPunkte(int punkte, int offen) => punkte + offen * MaxJeSpiel;

        /// <summary>1 + Teams, die selbst im schlechtesten Fall mehr Punkte haben als das Team im besten.</summary>
        public int BesterRang(int eigenMax) =>
            1 + Andere().Count(s => MinPunkte(s.Punkte, s.Offen) > eigenMax);

        /// <summary>1 + Teams, die im besten Fall mindestens so viele Punkte erreichen wie das Team im schlechtesten.</summary>
        public int SchlechtesterRang(int eigenMin) =>
            1 + Andere().Count(s => MaxPunkte(s.Punkte, s.Offen) >= eigenMin);

        private IEnumerable<Tabellenstand> Andere() => Tabelle.Where(s => s.MannschaftId != Eigen.MannschaftId);
    }

    // ── Zonen als Stufen ──────────────────────────────────────────────────

    /// <summary>Die Zonen der Rangfolge nach Rängen aufgereiht: Stufe 0 für Rang 1 und eine neue Stufe, sooft sich die Zone
    /// von einem Rang zum nächsten ändert (auch in „keine Zone“).</summary>
    private sealed class Zonenstufen
    {
        private readonly int[] _stufeJeRang;
        private readonly bool[] _vorbehaltJeRang;

        private Zonenstufen(int[] stufeJeRang, bool[] vorbehaltJeRang, int? ersterAbstiegsplatz, bool hatZone)
        {
            _stufeJeRang = stufeJeRang;
            _vorbehaltJeRang = vorbehaltJeRang;
            ErsterAbstiegsplatz = ersterAbstiegsplatz;
            HatZone = hatZone;
        }

        /// <summary>Ob überhaupt ein Rang der Gruppe in einer Zone liegt.</summary>
        public bool HatZone { get; }

        /// <summary>Erster Rang einer Abstiegszone (Tabellenbereich mit roter Farbe), sonst <c>null</c>.</summary>
        public int? ErsterAbstiegsplatz { get; }

        public int Stufe(int rang) => _stufeJeRang[rang - 1];

        public bool UnterVorbehalt(int rang) => _vorbehaltJeRang[rang - 1];

        public static Zonenstufen Aus(IReadOnlyList<TabellenZone> zonen, int? gruppenNummer, int anzahlTeams)
        {
            var stufe = new int[anzahlTeams];
            var vorbehalt = new bool[anzahlTeams];
            int? erster = null;
            TabellenZone? davor = null;
            int aktuell = 0;
            bool hatZone = false;
            for (int rang = 1; rang <= anzahlTeams; rang++)
            {
                var zone = TabellenZonen.ZoneFuer(zonen, gruppenNummer, rang);
                if (rang > 1 && !GleicheZone(davor, zone)) aktuell++;
                stufe[rang - 1] = aktuell;
                hatZone |= zone is not null;
                vorbehalt[rang - 1] = zone?.UnterVorbehalt ?? false;
                if (zone is { AusRegel: false, Farbe: ZonenFarbe.Rot } && erster is null) erster = rang;
                davor = zone;
            }
            return new Zonenstufen(stufe, vorbehalt, erster, hatZone);
        }

        private static bool GleicheZone(TabellenZone? a, TabellenZone? b) =>
            a is null || b is null
                ? a is null && b is null
                : (a.ZielRundeId, a.Bezeichnung, a.Farbe, a.AnzahlBeste) == (b.ZielRundeId, b.Bezeichnung, b.Farbe, b.AnzahlBeste);
    }
}
