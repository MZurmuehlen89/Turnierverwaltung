using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>Wer eine freie Hin-/Rückspiel-Paarung gewinnt – aus Sicht des Hinspiel-Heimteams.</summary>
public enum PaarungSieger { Keiner, Heim, Auswaerts }

/// <summary>Gesamtwertung der freien Hin-/Rückspiel-Simulation (#248, #509): Summenstand, Sieger und Zusatztext
/// („nach 180 Minuten“ …) aus Sicht des Hinspiel-Heimteams. Zustandslos.</summary>
public sealed record FreiePaarungGesamt(int HeimTore, int AuswaertsTore, PaarungSieger Sieger, string Zusatz)
{
    /// <param name="hinspiel">Hinspiel aus Sicht seines Heimteams.</param>
    /// <param name="rueckspiel">Rückspiel mit vertauschten Rollen: dessen „Heim“ ist das Hinspiel-Auswärtsteam.</param>
    public static FreiePaarungGesamt Bewerten(SimulationsErgebnis hinspiel, SimulationsErgebnis rueckspiel)
    {
        int heimGesamtRegulaer      = hinspiel.HeimToreRegulaer      + rueckspiel.AuswaertsToreRegulaer;
        int auswaertsGesamtRegulaer = hinspiel.AuswaertsToreRegulaer + rueckspiel.HeimToreRegulaer;

        int heimGesamt      = heimGesamtRegulaer      + (rueckspiel.AuswaertsToreVerlaengerung ?? 0);
        int auswaertsGesamt = auswaertsGesamtRegulaer + (rueckspiel.HeimToreVerlaengerung ?? 0);

        PaarungSieger sieger;
        if (heimGesamt != auswaertsGesamt)
            sieger = heimGesamt > auswaertsGesamt ? PaarungSieger.Heim : PaarungSieger.Auswaerts;
        else if (rueckspiel.EntscheidungArt == EntscheidungArt.Wiederholungsspiel && rueckspiel.Wiederholung is { } wdh)
            sieger = wdh.HeimGesamt > wdh.AuswaertsGesamt ? PaarungSieger.Auswaerts : PaarungSieger.Heim; // Rückspiel-"Heim" = Hinspiel-Auswärts (#547)
        else if (rueckspiel is { EntscheidungArt: EntscheidungArt.Elfmeterschiessen, HeimElfmetertore: not null, AuswaertsElfmetertore: not null })
            sieger = rueckspiel.HeimElfmetertore > rueckspiel.AuswaertsElfmetertore ? PaarungSieger.Auswaerts : PaarungSieger.Heim;
        else if (rueckspiel.LosGewinnerHeim is bool losFuerRueckspielHeim) // Losentscheid/Münzwurf (#383)
            sieger = losFuerRueckspielHeim ? PaarungSieger.Auswaerts : PaarungSieger.Heim;
        else
            sieger = PaarungSieger.Keiner;

        // Gesamtzeile aus Sicht des Hinspiel-Heimteams — das Rückspiel hat vertauschte Rollen, daher auch
        // den Elfmeterstand spiegeln.
        string zusatz = rueckspiel.EntscheidungArt switch
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

        return new FreiePaarungGesamt(heimGesamt, auswaertsGesamt, sieger, zusatz);
    }
}
