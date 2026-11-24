using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>#624: Eine K.-o.-Runde braucht ein wirksames Regelwerk mit Entscheidungsverfahren (Verlängerung,
/// Elfmeterschießen …), sonst endet ein Remis ohne Sieger und die Folgerunde bleibt offen. Wirksam ist
/// <c>runde.Regelwerk ?? TurnierInstanz.StandardRegelwerk</c> (wie in <c>SimulationsService</c>). Reine Funktionen,
/// damit Services, Validator und Formulare dieselbe Regel nutzen.</summary>
public static class KoRegelwerk
{
    /// <summary>KoRunde, Finale und Spiel um Platz 3 — die Rundenarten des KoRundeGenerators.</summary>
    public static bool IstKoRundenart(Rundenart rundenart) =>
        rundenart is Rundenart.KoRunde or Rundenart.Finale or Rundenart.SpielUmPlatzDrei;

    public static bool HatVerfahren(Regelwerk? regelwerk) =>
        regelwerk is not null && regelwerk.Entscheidungsverfahren.Count > 0;

    /// <summary>Ob eine Runde mit eigenem Regelwerk (oder ohne, dann gilt der Turnier-Standard) zulässig ist.</summary>
    public static bool RundeGeeignet(Rundenart rundenart, Regelwerk? eigenes, Regelwerk? standard) =>
        !IstKoRundenart(rundenart) || HatVerfahren(eigenes ?? standard);

    /// <summary>Die Regelwerke, die das Rundenformular für diese Rundenart anbietet: bei K.-o. nur mit Verfahren.</summary>
    public static IReadOnlyList<Regelwerk> Auswahl(Rundenart rundenart, IEnumerable<Regelwerk> alle) =>
        [.. IstKoRundenart(rundenart) ? alle.Where(HatVerfahren) : alle];

    /// <summary>„Kein Regelwerk“ ist bei K.-o. nur zulässig, wenn der Turnier-Standard ein Verfahren hat.</summary>
    public static bool KeinRegelwerkErlaubt(Rundenart rundenart, Regelwerk? standard) =>
        !IstKoRundenart(rundenart) || HatVerfahren(standard);

    /// <summary>Fehlermeldung zu einer Runde, <c>null</c> wenn sie geeignet ist.</summary>
    public static string? Fehler(string rundenName, Rundenart rundenart, Regelwerk? eigenes, Regelwerk? standard)
    {
        if (RundeGeeignet(rundenart, eigenes, standard)) return null;
        var wirksam = eigenes ?? standard;
        return wirksam is null
            ? $"K.-o.-Runde '{rundenName}' benötigt ein Regelwerk mit Entscheidungsverfahren (z. B. Verlängerung, Elfmeterschießen), hat aber keins."
            : $"K.-o.-Runde '{rundenName}' benötigt einen Sieger, aber Regelwerk '{wirksam.Name}' hat kein Entscheidungsverfahren definiert (z. B. Verlängerung, Elfmeterschießen).";
    }
}
