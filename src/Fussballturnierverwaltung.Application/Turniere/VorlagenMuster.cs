using Fussballturnierverwaltung.Application.Spielplan.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Eine Turniervorlage als Muster für <see cref="UebergangsVorschlaege"/> (#167, Quelle A): ihre Runden in
/// Reihenfolge und ihre Übergangsregeln über die 1-basierte Position der Runden.</summary>
public sealed record VorlagenMuster(
    string Name,
    IReadOnlyList<TurnierRunde> Runden,
    IReadOnlyList<UebergangsregelPositionsEingabe> Regeln,
    int InstanzId = 0)
{
    /// <summary>Baut das Muster aus einer geladenen Vorlage und ihren Übergangsregeln (Quell- und Zielrunde nur über
    /// ihre Ids).</summary>
    public static VorlagenMuster Aus(TurnierInstanz vorlage, IEnumerable<Uebergangsregel> uebergangsregeln)
    {
        var runden = vorlage.Runden.OrderBy(r => r.Reihenfolge).ToList();
        var position = runden.Select((r, index) => (r.Id, Position: index + 1)).ToDictionary(x => x.Id, x => x.Position);
        var regeln = uebergangsregeln
            .Where(r => position.ContainsKey(r.QuellRundeId) && position.ContainsKey(r.ZielRundeId))
            .Select(r => new UebergangsregelPositionsEingabe(
                position[r.QuellRundeId], position[r.ZielRundeId], r.GruppeIndex, r.PlatzierungVon, r.PlatzierungBis, r.AnzahlBeste))
            .ToList();
        return new VorlagenMuster(vorlage.Name, runden, regeln, vorlage.Id);
    }

    /// <summary>Die Rundenfolge bis einschließlich <paramref name="zielIndex"/> stimmt mit der des Musters überein
    /// (Rundenart, Eingang, Ausgang, Gruppenanzahl); bei Abweichung gibt es keinen Vorschlag aus dieser Vorlage.</summary>
    public bool PasstBis(IReadOnlyList<TurnierRunde> eigeneRunden, int zielIndex)
    {
        if (Runden.Count <= zielIndex || eigeneRunden.Count <= zielIndex) return false;
        for (int i = 0; i <= zielIndex; i++)
        {
            var (eigen, muster) = (eigeneRunden[i], Runden[i]);
            if (eigen.Rundenart != muster.Rundenart
                || eigen.EingangsMannschaftsanzahl != muster.EingangsMannschaftsanzahl
                || eigen.AusgangsMannschaftsanzahl != muster.AusgangsMannschaftsanzahl
                || eigen.GruppenAnzahl != muster.GruppenAnzahl)
                return false;
        }
        return true;
    }
}
