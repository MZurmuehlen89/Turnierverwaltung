using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Farbschlüssel einer Tabellenzone (Issue #358) — die Oberfläche bildet ihn auf Theme-Farben ab.
/// Vier Qualifikationsfarben, <see cref="Amber"/> für „unter Vorbehalt“ (z. B. Gruppendritte, Relegation),
/// <see cref="Rot"/> für Ausscheiden/Abstieg.</summary>
public enum ZonenFarbe
{
    Gruen,
    Blau,
    Tuerkis,
    Violett,
    Amber,
    Rot
}

/// <summary>Platzierungsbereich einer Tabelle, der in eine Zielrunde führt (Issue #358). Bei
/// <see cref="AnzahlBeste"/> kommen nur die Besten dieses Platzes über alle Gruppen hinweg weiter — der Platz
/// allein sichert das Weiterkommen dann nicht.</summary>
public sealed record TabellenZone(
    int? GruppeIndex, int PlatzVon, int PlatzBis, int ZielRundeId, string ZielRundeName, ZonenFarbe Farbe, int? AnzahlBeste = null)
{
    public bool UnterVorbehalt => AnzahlBeste is not null;

    public bool Umfasst(int? gruppenNummer, int rang) =>
        (GruppeIndex is null || GruppeIndex == gruppenNummer) && rang >= PlatzVon && rang <= PlatzBis;
}

public sealed record ZonenLegendenEintrag(ZonenFarbe Farbe, string Text);

public static class TabellenZonen
{
    private static readonly ZonenFarbe[] QUALIFIKATIONS_FARBEN =
        [ZonenFarbe.Gruen, ZonenFarbe.Blau, ZonenFarbe.Tuerkis, ZonenFarbe.Violett];

    /// <summary>Leitet die Zonen einer Runde aus ihren ausgehenden Übergangsregeln ab (<c>ZielRunde</c> muss
    /// geladen sein). Jede Zielrunde bekommt eine Qualifikationsfarbe, vergeben in der Reihenfolge der besten
    /// Platzierung; gruppenübergreifende Regeln („die n besten Dritten“) sind Zonen unter Vorbehalt.</summary>
    public static IReadOnlyList<TabellenZone> AusRegeln(IEnumerable<Uebergangsregel> ausgehendeRegeln)
    {
        var regeln = ausgehendeRegeln.ToList();
        var farbeJeZielRunde = regeln
            .Where(r => r.AnzahlBeste is null)
            .GroupBy(r => r.ZielRundeId)
            .OrderBy(g => g.Min(r => r.PlatzierungVon))
            .Select((g, index) => (ZielRundeId: g.Key, Farbe: QUALIFIKATIONS_FARBEN[index % QUALIFIKATIONS_FARBEN.Length]))
            .ToDictionary(z => z.ZielRundeId, z => z.Farbe);

        return [.. regeln
            .OrderBy(r => r.PlatzierungVon).ThenBy(r => r.GruppeIndex)
            .Select(r => new TabellenZone(
                r.GruppeIndex, r.PlatzierungVon, r.PlatzierungBis, r.ZielRundeId, r.ZielRunde.Name,
                r.AnzahlBeste is null ? farbeJeZielRunde[r.ZielRundeId] : ZonenFarbe.Amber,
                r.AnzahlBeste))];
    }

    /// <summary>Farbe, in der das tatsächliche Erreichen der Zielrunde einer Vorbehalts-Zone markiert wird: die
    /// Farbe der direkten Zone derselben Zielrunde.</summary>
    public static ZonenFarbe ZielFarbe(IReadOnlyList<TabellenZone> zonen, int zielRundeId) =>
        zonen.FirstOrDefault(z => !z.UnterVorbehalt && z.ZielRundeId == zielRundeId)?.Farbe ?? ZonenFarbe.Gruen;

    /// <summary>Setzt je Eintrag die Zonenfarbe des aktuellen Rangs und — mit <paramref name="mitSicherheit"/> —
    /// die Punktfarbe, sobald die Zone feststeht: alle noch möglichen Ränge
    /// (<see cref="TabellenEintrag.BesterRang"/> bis <see cref="TabellenEintrag.SchlechtesterRang"/>) führen in
    /// dieselbe Zielrunde (Zonenfarbe) bzw. in keine (<see cref="ZonenFarbe.Rot"/>). Ein sicherer Platz in einer
    /// Zone unter Vorbehalt bekommt keinen Punkt, weil das Weiterkommen dort erst der Vergleich über alle Gruppen
    /// entscheidet.</summary>
    public static void Zuweisen(
        IEnumerable<TabellenEintrag> tabelle, IReadOnlyList<TabellenZone> zonen, int? gruppenNummer, bool mitSicherheit)
    {
        TabellenZone? Zone(int rang) => zonen.FirstOrDefault(z => z.Umfasst(gruppenNummer, rang));

        foreach (var eintrag in tabelle)
        {
            eintrag.ZonenFarbe = Zone(eintrag.Rang)?.Farbe;
            eintrag.PunktFarbe = null;

            if (!mitSicherheit || zonen.Count == 0) continue;
            if (eintrag.BesterRang is not { } bester || eintrag.SchlechtesterRang is not { } schlechtester) continue;

            var moeglich = Enumerable.Range(bester, Math.Max(1, schlechtester - bester + 1))
                .Select(Zone)
                .Select(z => (z?.ZielRundeId, UnterVorbehalt: z?.UnterVorbehalt ?? false, z?.Farbe))
                .Distinct()
                .ToList();
            if (moeglich.Count != 1 || moeglich[0].UnterVorbehalt) continue;

            eintrag.PunktFarbe = moeglich[0].Farbe ?? ZonenFarbe.Rot;
        }
    }

    /// <summary>Markiert die gruppenübergreifende Rangfolge einer Regel „die n Besten“: Die Plätze der Besten
    /// tragen die Farbe der Zielrunde. Stehen alle Qualifikanten fest (<paramref name="qualifikanten"/> ohne
    /// <c>null</c>), bekommt jeder Eintrag Status und Punkt — Zielfarbe für Qualifizierte,
    /// <see cref="ZonenFarbe.Rot"/> für Ausgeschiedene.</summary>
    public static void BesteZuweisen(
        IReadOnlyList<TabellenEintrag> rangfolge, IReadOnlyList<TabellenZone> zonen, int zielRundeId, int anzahlBeste,
        IEnumerable<int?> qualifikanten)
    {
        // Issue #358: Hier fällt die Entscheidung über das Weiterkommen — die Plätze der Besten tragen die
        // Farbe der Zielrunde, der Punkt folgt, sobald die Qualifikanten feststehen.
        var zielFarbe = ZielFarbe(zonen, zielRundeId);
        foreach (var eintrag in rangfolge)
            eintrag.ZonenFarbe = eintrag.Rang <= anzahlBeste ? zielFarbe : null;

        if (qualifikanten.All(v => v.HasValue))
        {
            var qualifizierteIds = qualifikanten.Select(v => v!.Value).ToHashSet();
            foreach (var eintrag in rangfolge)
            {
                bool qualifiziert = qualifizierteIds.Contains(eintrag.Mannschaft.Id);
                eintrag.Qualifikationsstatus = qualifiziert
                    ? Qualifikationsstatus.SicherQualifiziert
                    : Qualifikationsstatus.SicherAusgeschieden;
                eintrag.SicherPositioniert = true;
                eintrag.PunktFarbe = qualifiziert ? zielFarbe : ZonenFarbe.Rot;
            }
        }
    }

    public static IReadOnlyList<ZonenLegendenEintrag> Legende(IReadOnlyList<TabellenZone> zonen) =>
        [.. zonen
            .Select(z => new ZonenLegendenEintrag(z.Farbe, LegendenText(z)))
            .Distinct()];

    private static string LegendenText(TabellenZone zone)
    {
        string platz = zone.PlatzVon == zone.PlatzBis ? $"Platz {zone.PlatzVon}" : $"Platz {zone.PlatzVon}–{zone.PlatzBis}";
        if (!zone.UnterVorbehalt) return $"{platz} → {zone.ZielRundeName}";
        return zone.AnzahlBeste == 1
            ? $"{platz} → {zone.ZielRundeName} (nur der beste)"
            : $"{platz} → {zone.ZielRundeName} (nur die {zone.AnzahlBeste} besten)";
    }
}
