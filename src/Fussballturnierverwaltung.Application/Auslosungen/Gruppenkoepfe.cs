using Fussballturnierverwaltung.Domain.Entitaeten.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Feste Gruppenköpfe der Gruppenphase (Issue #487): Gastgeber und Titelverteidiger führen die an der Runde
/// vorgegebenen Gruppen an (EM 2024: Gastgeber A; WM 2026: Gastgeber A, B, D; Copa América 2024: Titelverteidiger A,
/// Gastgeber C). Das Programm legt sie selbst in Topf 1 (beim Anlegen der Töpfe, nach dem Zurücksetzen und spätestens
/// beim Auslosen) und behandelt sie beim Auslosen wie Gesetzte.
/// Gruppen zählen ab 1 (A = 1).</summary>
public static class Gruppenkoepfe
{
    /// <summary>Gruppe (ab 0) je Mannschafts-Id. Die Gastgeber bekommen die Gastgeber-Gruppen in der Reihenfolge der
    /// Gastgeberliste, überzählige Gastgeber keine. Der Titelverteidiger bekommt seine Gruppe nur, wenn er nicht
    /// selbst Gastgeber ist.</summary>
    public static IReadOnlyDictionary<int, int> Bestimmen(
        TurnierRunde runde, IReadOnlyList<int> gastgeberIds, int? titelverteidigerId)
    {
        var gruppeJeMannschaft = new Dictionary<int, int>();
        if (runde.Rundenart != Rundenart.Gruppenphase) return gruppeJeMannschaft;

        foreach (var (gastgeber, gruppe) in gastgeberIds.Zip(runde.GastgeberGruppen))
            gruppeJeMannschaft.TryAdd(gastgeber, gruppe - 1);
        if (titelverteidigerId is { } tv && runde.TitelverteidigerGruppe is { } tvGruppe)
            gruppeJeMannschaft.TryAdd(tv, tvGruppe - 1);
        return gruppeJeMannschaft;
    }

    /// <summary>Wer beim automatischen Befüllen vorab in Topf 1 kommt: die festen Gruppenköpfe und — mit „Titelverteidiger
    /// in Topf 1“ — der Titelverteidiger.</summary>
    public static IReadOnlyList<int> VorabInTopf1(TurnierRunde runde, IReadOnlyList<int> gastgeberIds, int? titelverteidigerId)
    {
        var koepfe = Bestimmen(runde, gastgeberIds, titelverteidigerId);
        var vorab  = gastgeberIds.Where(koepfe.ContainsKey).ToList();
        if (titelverteidigerId is { } tv && (koepfe.ContainsKey(tv) || runde.TitelverteidigerInTopf1) && !vorab.Contains(tv))
            vorab.Add(tv);
        return vorab;
    }

    /// <summary>Legt die Gruppenköpfe, die noch in keinem Lostopf liegen, in den ersten Topf — solange die Töpfe
    /// zusammen weniger als <paramref name="hoechstens"/> Teams enthalten (<c>null</c> oder 0 = ohne Grenze).
    /// Liefert, ob ein Topf sich geändert hat, und die Köpfe, die keinen Platz mehr fanden.</summary>
    public static (bool Geaendert, IReadOnlyList<int> OhnePlatz) Einlegen(
        IReadOnlyDictionary<int, int> koepfe, IEnumerable<Lostopf> lostoepfe, int? hoechstens)
    {
        var toepfe = lostoepfe.OrderBy(l => l.Reihenfolge).ToList();
        if (toepfe.Count == 0) return (false, [.. koepfe.Keys]);

        var eintraege = toepfe.SelectMany(l => l.Eintraege).ToList();
        var fehlende  = koepfe.Keys.Where(id => !eintraege.Any(e => e.MannschaftId == id)).ToList();
        int frei      = hoechstens is > 0 ? Math.Max(0, hoechstens.Value - eintraege.Count) : fehlende.Count;

        var eingelegt = fehlende.Take(frei).ToList();
        foreach (int id in eingelegt)
            toepfe[0].Eintraege.Add(new LostopfEintrag { LostopfId = toepfe[0].Id, MannschaftId = id });
        return (eingelegt.Count > 0, [.. fehlende.Skip(eingelegt.Count)]);
    }

    public static string Buchstabe(int gruppe) => GruppenHerkunft.Buchstabe(gruppe);

    public static string AlsText(IEnumerable<int> gruppen) => string.Join(", ", gruppen.Select(Buchstabe));

    /// <summary>Liest „A, B, D“ (auch „ABD“, „a b d“) als Gruppen ab 1. Leer ergibt eine leere Liste. Liefert
    /// <c>null</c> und einen Fehlertext bei unbekannten Zeichen, doppelten Gruppen oder einer Gruppe jenseits von
    /// <paramref name="gruppenAnzahl"/>.</summary>
    public static IReadOnlyList<int>? AusText(string? text, int? gruppenAnzahl, out string? fehler)
    {
        fehler = null;
        var gruppen = new List<int>();
        foreach (char zeichen in (text ?? "").ToUpperInvariant())
        {
            if (zeichen is ',' or ';' || char.IsWhiteSpace(zeichen)) continue;
            if (zeichen is < 'A' or > 'Z')
            {
                fehler = $"„{zeichen}“ ist keine Gruppe — Gruppen als Buchstaben angeben, z. B. „A, B, D“.";
                return null;
            }
            int gruppe = zeichen - 'A' + 1;
            if (gruppen.Contains(gruppe))
            {
                fehler = $"Gruppe {zeichen} ist doppelt angegeben.";
                return null;
            }
            if (gruppe > gruppenAnzahl)
            {
                fehler = $"Gruppe {zeichen} gibt es bei {gruppenAnzahl} Gruppen nicht.";
                return null;
            }
            gruppen.Add(gruppe);
        }
        return gruppen;
    }

    /// <summary>Widersprüche zwischen den Vorgaben einer Runde, oder <c>null</c>.</summary>
    public static string? Widerspruch(IReadOnlyList<int> gastgeberGruppen, int? titelverteidigerGruppe, int? gruppenAnzahl)
    {
        if (gastgeberGruppen.Concat(titelverteidigerGruppe is { } t ? [t] : []).Any(g => g < 1 || g > gruppenAnzahl))
            return $"Die Gruppenköpfe nennen eine Gruppe, die es bei {gruppenAnzahl ?? 0} Gruppen nicht gibt.";
        if (titelverteidigerGruppe is { } tv && gastgeberGruppen.Contains(tv))
            return $"Gruppe {Buchstabe(tv)} ist für Gastgeber und Titelverteidiger zugleich vorgesehen.";
        return null;
    }
}
