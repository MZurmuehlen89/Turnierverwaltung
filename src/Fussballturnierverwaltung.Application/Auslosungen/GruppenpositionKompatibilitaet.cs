using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Wann die Auswahl der <see cref="Gruppenposition"/> wirkt (Issue #475) — eine Quelle für das
/// Losverfahren und für die Oberfläche, die die Auswahl sonst mit genau diesem Grund ausgraut.</summary>
public static class GruppenpositionKompatibilitaet
{
    public static IReadOnlyList<Gruppenposition> Varianten { get; } = Enum.GetValues<Gruppenposition>();

    public static string AnzeigeName(Gruppenposition variante) => variante switch
    {
        Gruppenposition.NachTopf   => "Nach Topf (Topf 1 → Position 1, Topf 2 → Position 2 …)",
        Gruppenposition.Topf1Fest  => "Topf 1 fest, übrige Positionen gelost",
        Gruppenposition.AlleGelost => "Alle Positionen gelost",
        _                          => variante.ToString()
    };

    /// <summary>Liefert den Grund, warum die Auswahl in dieser Runde keine Wirkung hätte, oder <c>null</c>.
    /// <paramref name="art"/> = null: Auslosungsart noch frei (Vorgabe im Rundeneditor).</summary>
    public static string? SperrGrund(Rundenart? rundenart, Auslosungsart? art)
    {
        if (rundenart == Rundenart.Liga)
            return "In einer Liga wird immer die gesamte Reihenfolge gelost — sie ist die Schlüsselzahl des Spielplans.";
        if (rundenart != Rundenart.Gruppenphase)
            return "Die Position in der Gruppe gibt es nur in der Gruppenphase — in dieser Runde werden keine " +
                   "Gruppen ausgelost.";

        return art switch
        {
            null or Auslosungsart.NachToepfen or Auslosungsart.NachSetzliste => null,
            Auslosungsart.Manuell =>
                "Bei der manuellen Auslosung legst du die Position beim Zuweisen selbst fest.",
            _ => "Ohne Töpfe gibt es keinen Topf, an den die Position gebunden wäre — sie wird immer gelost."
        };
    }

    /// <summary>Die beim Auslosen tatsächlich wirksame Variante: ohne Töpfe immer gelost, sonst die gewählte.</summary>
    public static Gruppenposition Wirksam(Auslosungsart art, Gruppenposition gewaehlt) =>
        art is Auslosungsart.NachToepfen or Auslosungsart.NachSetzliste ? gewaehlt : Gruppenposition.AlleGelost;
}
