using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Wer eine freie Hin-/Rückspiel-Paarung gewinnt – aus Sicht des Hinspiel-Heimteams.</summary>
public enum PaarungSieger { Keiner, Heim, Auswaerts }

/// <summary>Gesamtwertung der freien Hin-/Rückspiel-Simulation (#248, #509): Summenstand, Sieger und Zusatztext
/// („nach 180 Minuten“ …) aus Sicht des Hinspiel-Heimteams. Zustandslos.</summary>
/// <remarks>Bewusst nicht über <c>SiegerErmittlung</c> gelöst: bei torgleichem Wiederholungsspiel gibt diese keinen Sieger
/// an, hier fällt er auf das Hinspiel-Heimteam. Im Spiel ist das nicht erreichbar (die Simulation wirft dort, #547),
/// ein Angleichen wäre aber eine Verhaltensänderung und gehört nicht in #509.</remarks>
public sealed record FreiePaarungGesamt(int HeimTore, int AuswaertsTore, PaarungSieger Sieger, string Zusatz)
{
    /// <param name="hinspiel">Hinspiel aus Sicht seines Heimteams.</param>
    /// <param name="rueckspiel">Rückspiel mit vertauschten Rollen: dessen „Heim“ ist das Hinspiel-Auswärtsteam.</param>
    public static FreiePaarungGesamt Bewerten(SimulationsErgebnis hinspiel, SimulationsErgebnis rueckspiel)
    {
        int heimGesamt      = hinspiel.HeimToreRegulaer      + rueckspiel.AuswaertsToreRegulaer + (rueckspiel.AuswaertsToreVerlaengerung ?? 0);
        int auswaertsGesamt = hinspiel.AuswaertsToreRegulaer + rueckspiel.HeimToreRegulaer      + (rueckspiel.HeimToreVerlaengerung ?? 0);

        var sieger = heimGesamt != auswaertsGesamt
            ? (heimGesamt > auswaertsGesamt ? PaarungSieger.Heim : PaarungSieger.Auswaerts)
            : SiegerNachVerfahren(rueckspiel);
        return new FreiePaarungGesamt(heimGesamt, auswaertsGesamt, sieger, Zusatztext(rueckspiel));
    }

    // Gesamtstand ausgeglichen: das Verfahren des Rückspiels entscheidet. Dessen „Heim“ ist das Hinspiel-Auswärtsteam.
    private static PaarungSieger SiegerNachVerfahren(SimulationsErgebnis rueckspiel)
    {
        if (rueckspiel.EntscheidungArt == EntscheidungArt.Wiederholungsspiel && rueckspiel.Wiederholung is { } wdh)
            return wdh.HeimGesamt > wdh.AuswaertsGesamt ? PaarungSieger.Auswaerts : PaarungSieger.Heim; // #547
        if (rueckspiel is { EntscheidungArt: EntscheidungArt.Elfmeterschiessen, HeimElfmetertore: { } heimE, AuswaertsElfmetertore: { } auswaertsE })
            return heimE > auswaertsE ? PaarungSieger.Auswaerts : PaarungSieger.Heim;
        if (rueckspiel.LosGewinnerHeim is bool losFuerRueckspielHeim) // Losentscheid/Münzwurf (#383)
            return losFuerRueckspielHeim ? PaarungSieger.Auswaerts : PaarungSieger.Heim;
        return PaarungSieger.Keiner;
    }

    // Gesamtzeile aus Sicht des Hinspiel-Heimteams — das Rückspiel hat vertauschte Rollen, daher auch
    // den Elfmeterstand spiegeln.
    private static string Zusatztext(SimulationsErgebnis rueckspiel) => rueckspiel.EntscheidungArt switch
    {
        EntscheidungArt.Elfmeterschiessen => $"nach Elfmeterschießen ({rueckspiel.AuswaertsElfmetertore}:{rueckspiel.HeimElfmetertore})",
        EntscheidungArt.Verlaengerung
            or EntscheidungArt.GoldenGoal
            or EntscheidungArt.SilverGoal => "nach Verlängerung",
        EntscheidungArt.Auswaertstorregel => "nach Auswärtstorregel",
        EntscheidungArt.Wiederholungsspiel => "nach Wiederholungsspiel",
        EntscheidungArt.Losentscheid      => "nach Losentscheid",
        EntscheidungArt.Muenzwurf         => "nach Münzwurf",
        _                                 => "nach 180 Minuten"
    };
}
