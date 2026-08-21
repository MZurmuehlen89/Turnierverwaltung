using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spiele;

/// <summary>
/// Formatiert das Entscheidungsart-Kuerzel eines KO-Ergebnisses nach gaengiger
/// Sportportal-Konvention: Regulaerzeit-Stand nur in Klammern, wenn er vom
/// (bereits kumulierten) Endstand abweicht, gefolgt vom Entscheidungs-Kuerzel.
/// </summary>
public static class SpielErgebnisFormatierer
{
    public static string DetailText(
        EntscheidungArt entscheidungArt,
        int heimToreRegulaer, int auswaertsToreRegulaer,
        int? heimToreVerlaengerung, int? auswaertsToreVerlaengerung,
        int? heimElfmetertore, int? auswaertsElfmetertore)
    {
        if (entscheidungArt == EntscheidungArt.Auswaertstorregel) return "A.T.";

        bool istVerlaengerungsFall = entscheidungArt is
            EntscheidungArt.Verlaengerung or EntscheidungArt.GoldenGoal or
            EntscheidungArt.SilverGoal or EntscheidungArt.Elfmeterschiessen;
        if (!istVerlaengerungsFall) return "";

        int endHeim      = heimToreRegulaer      + (heimToreVerlaengerung ?? 0);
        int endAuswaerts = auswaertsToreRegulaer + (auswaertsToreVerlaengerung ?? 0);
        string regulaerKlammer = (endHeim != heimToreRegulaer || endAuswaerts != auswaertsToreRegulaer)
            ? $"({heimToreRegulaer}:{auswaertsToreRegulaer}) "
            : "";

        if (entscheidungArt == EntscheidungArt.Elfmeterschiessen)
            return $"{regulaerKlammer}i.E. {heimElfmetertore}:{auswaertsElfmetertore}";

        string marker = entscheidungArt switch
        {
            EntscheidungArt.GoldenGoal => "G.G.",
            EntscheidungArt.SilverGoal => "S.G.",
            _                          => "n.V."
        };
        return $"{regulaerKlammer}{marker}";
    }

    /// <summary>
    /// Formatiert den Halbzeitstand in Klammern (z.B. "(1:0)") für die Ergebnisanzeige, analog zur
    /// gängigen Sportportal-Konvention "2:1 (1:0)" (Issue #104). Nur bei reinen Regulärzeit-Spielen —
    /// bei Verlängerung/Elfmeterschießen/Auswärtstorregel steht in derselben Anzeige-Position bereits
    /// das <see cref="DetailText"/>-Kürzel. Leer, wenn kein Halbzeitstand erfasst wurde oder er dem
    /// Regulärzeit-Endstand entspricht (sonst wäre die Klammer redundant).
    /// </summary>
    public static string HalbzeitKlammer(
        EntscheidungArt entscheidungArt,
        int heimToreRegulaer, int auswaertsToreRegulaer,
        int? halbzeitHeimTore, int? halbzeitAuswaertsTore)
    {
        if (entscheidungArt != EntscheidungArt.RegulaereSpielzeit) return "";
        if (halbzeitHeimTore is null || halbzeitAuswaertsTore is null) return "";
        if (halbzeitHeimTore == heimToreRegulaer && halbzeitAuswaertsTore == auswaertsToreRegulaer) return "";

        return $"({halbzeitHeimTore}:{halbzeitAuswaertsTore})";
    }
}
