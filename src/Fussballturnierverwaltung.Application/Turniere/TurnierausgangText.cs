using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Texte zum <see cref="Turnierausgang"/> (#527): wodurch ein Platz feststeht und was noch offen ist.</summary>
public static class TurnierausgangText
{
    public const string TURNIER_LAEUFT = "Noch offen – das Turnier läuft.";

    /// <summary>Wodurch der Platz feststeht, z. B. „Finale gewonnen (i.E.)“ oder beim Meister die Spieltage.</summary>
    public static string Erlaeuterung(Platzierung platzierung, Turnierausgang ausgang) => platzierung.Grund switch
    {
        PlatzierungsGrund.Meister            => MeisterErlaeuterung(ausgang),
        // Beim geteilten Platz 3 sagt die Entscheidungsart des Halbfinales nichts über den Platz aus.
        PlatzierungsGrund.HalbfinaleVerloren => "Halbfinale",
        _                                    => MitKuerzel(KoGrund(platzierung.Grund), platzierung.Entscheidung)
    };

    /// <summary>Was über den noch offenen Sieger bekannt ist, <c>null</c>, wenn er feststeht.</summary>
    public static string? Hinweis(Turnierausgang ausgang)
    {
        if (ausgang.Sieger is not null) return null;
        return ausgang.Art switch
        {
            TurnierausgangArt.Finale when ausgang.Finalisten is [var a, var b]
                => $"{a.Name} oder {b.Name} – das Finale ist noch nicht gespielt.",
            TurnierausgangArt.Tabelle when ausgang.Tabellenfuehrer is { } fuehrer
                => TabellenHinweis(ausgang, fuehrer),
            _ => TURNIER_LAEUFT
        };
    }

    private static string KoGrund(PlatzierungsGrund grund) => grund switch
    {
        PlatzierungsGrund.FinaleGewonnen           => "Finale gewonnen",
        PlatzierungsGrund.FinaleVerloren           => "Finale verloren",
        PlatzierungsGrund.SpielUmPlatzDreiGewonnen => "Spiel um Platz 3 gewonnen",
        _                                          => "Spiel um Platz 3 verloren"
    };

    private static string MitKuerzel(string grund, EntscheidungArt? art)
    {
        string kuerzel = art is { } a ? SpielErgebnisFormatierer.EntscheidungKuerzel(a) : "";
        return kuerzel.Length > 0 ? $"{grund} ({kuerzel})" : grund;
    }

    private static string MeisterErlaeuterung(Turnierausgang ausgang) => (ausgang.OffeneSpieltage, ausgang.SpieltageGesamt) switch
    {
        (_, 0)      => "Meister",
        (0, var n)  => $"Nach {n} Spieltagen",
        var (o, n)  => $"Steht vorzeitig fest, noch {o} von {n} Spieltagen offen"
    };

    private static string TabellenHinweis(Turnierausgang ausgang, Mannschaft fuehrer) => ausgang.SpieltageGesamt > 0
        ? $"Noch offen – {ausgang.OffeneSpieltage} von {ausgang.SpieltageGesamt} Spieltagen stehen aus. Tabellenführer: {fuehrer.Name}."
        : $"Noch offen. Tabellenführer: {fuehrer.Name}.";
}
