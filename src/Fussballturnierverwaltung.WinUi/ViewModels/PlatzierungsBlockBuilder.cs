using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Ein Platz im Block „Platzierungen“; <paramref name="Erlaeuterung"/> nennt, wodurch er feststeht.</summary>
public record PlatzierungsZeile(int Platz, Mannschaft Mannschaft, string Erlaeuterung)
{
    public string PlatzText => $"{Platz}.";
    public bool IstSieger => Platz == 1;
    public double NamensGroesse => IstSieger ? 16 : 13;

    /// <summary>Der Sieger bekommt eine eigene Kennung, damit GUI-Tests ihn direkt lesen können (#527).</summary>
    public string AutomationId => IstSieger ? "Statistik.Turniersieger" : "Statistik.Platzierung";
}

/// <summary>Block „Platzierungen“ bzw. „Meister“ oben auf der Turnierseite der Statistik (#527).
/// <paramref name="Hinweis"/> steht, solange der Sieger offen ist.</summary>
public record PlatzierungsBlock(string Titel, IReadOnlyList<PlatzierungsZeile> Zeilen, string? Hinweis)
{
    public bool HatHinweis => Hinweis is not null;
}

/// <summary>Baut den <see cref="PlatzierungsBlock"/> aus dem <see cref="Turnierausgang"/>.</summary>
public static class PlatzierungsBlockBuilder
{
    private const string HINWEIS_TURNIER_LAEUFT = "Noch offen – das Turnier läuft.";

    /// <summary><c>null</c>, wenn das Turnier keinen Sieger kürt.</summary>
    public static PlatzierungsBlock? Erstellen(Turnierausgang ausgang) => ausgang.Art switch
    {
        TurnierausgangArt.Finale  => new PlatzierungsBlock("Platzierungen",
            [.. ausgang.Plaetze.Select(p => new PlatzierungsZeile(p.Platz, p.Mannschaft, KoErlaeuterung(p)))],
            ausgang.Sieger is null ? FinalHinweis(ausgang.Finalisten) : null),
        TurnierausgangArt.Tabelle => new PlatzierungsBlock("Meister",
            [.. ausgang.Plaetze.Select(p => new PlatzierungsZeile(p.Platz, p.Mannschaft, MeisterErlaeuterung(ausgang)))],
            ausgang.Sieger is null ? TabellenHinweis(ausgang) : null),
        _ => null
    };

    private static string KoErlaeuterung(Platzierung p)
    {
        string grund = p.Grund switch
        {
            PlatzierungsGrund.FinaleGewonnen           => "Finale gewonnen",
            PlatzierungsGrund.FinaleVerloren           => "Finale verloren",
            PlatzierungsGrund.SpielUmPlatzDreiGewonnen => "Spiel um Platz 3 gewonnen",
            PlatzierungsGrund.SpielUmPlatzDreiVerloren => "Spiel um Platz 3 verloren",
            _                                          => "Halbfinale"
        };
        // Beim Halbfinale sagt die Entscheidungsart nichts über den geteilten Platz aus.
        string kuerzel = p.Grund == PlatzierungsGrund.HalbfinaleVerloren || p.Entscheidung is not EntscheidungArt art
            ? ""
            : SpielErgebnisFormatierer.EntscheidungKuerzel(art);
        return kuerzel.Length > 0 ? $"{grund} ({kuerzel})" : grund;
    }

    private static string FinalHinweis(IReadOnlyList<Mannschaft> finalisten) => finalisten.Count == 2
        ? $"{finalisten[0].Name} oder {finalisten[1].Name} – das Finale ist noch nicht gespielt."
        : HINWEIS_TURNIER_LAEUFT;

    private static string MeisterErlaeuterung(Turnierausgang ausgang) => ausgang.OffeneSpieltage > 0
        ? $"Steht vorzeitig fest, noch {ausgang.OffeneSpieltage} von {ausgang.SpieltageGesamt} Spieltagen offen"
        : $"Nach {ausgang.SpieltageGesamt} Spieltagen";

    private static string TabellenHinweis(Turnierausgang ausgang) => ausgang.Tabellenfuehrer is { } fuehrer
        ? $"Noch offen – {ausgang.OffeneSpieltage} von {ausgang.SpieltageGesamt} Spieltagen stehen aus. Tabellenführer: {fuehrer.Name}."
        : HINWEIS_TURNIER_LAEUFT;
}
