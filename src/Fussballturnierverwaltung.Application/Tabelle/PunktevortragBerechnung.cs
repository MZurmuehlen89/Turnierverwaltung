using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Der Startwert eines Teams in der Tabelle einer Folgerunde (#355): die übernommenen Punkte und der
/// ungerundete Wert. Beide Werte brauchen Tabelle und Rangfolge – bei Punktgleichheit entscheidet der ungerundete Wert
/// vor allen Kriterien des Regelsets.</summary>
/// <param name="Punkte">Ganze Punkte, mit denen die Tabelle startet.</param>
/// <param name="Ungerundet">Der Wert vor dem Runden (bei halbierten Modi in halben Punkten).</param>
/// <param name="QuellPunkte">Die Punkte des Teams in der Quellrunde, aus denen der Vortrag entstand.</param>
public readonly record struct Vortragswert(int Punkte, decimal Ungerundet, int QuellPunkte = 0)
{
    public static readonly Vortragswert Keiner = new(0, 0m);

    /// <summary>Wurde der Wert beim Halbieren gerundet (ungerade Punktzahl)?</summary>
    public bool IstGerundet => Ungerundet != Punkte;

    /// <summary>Wurde abgerundet – dann liegt der ganze Wert unter dem ungerundeten.</summary>
    public bool IstAbgerundet => Punkte < Ungerundet;

    /// <summary>Wurde aufgerundet – dann liegt der ganze Wert über dem ungerundeten.</summary>
    public bool IstAufgerundet => Punkte > Ungerundet;

    /// <summary>Der Wert für die Spalte „Vortrag“: die Punkte, bei gerundetem Wert mit Pfeil (↓ abgerundet, ↑ aufgerundet).</summary>
    public string Anzeige => $"{Punkte}{(IstAbgerundet ? "↓" : IstAufgerundet ? "↑" : "")}";

    /// <summary>Die Herleitung für den Tooltip, etwa „31 Punkte in der Quellrunde, halbiert 15,5 → 15“.</summary>
    public string Beschreibung => IstHalbiert
        ? $"{QuellPunkte} Punkte in der Quellrunde, halbiert {Ungerundet.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))} → {Punkte}"
        : $"{QuellPunkte} Punkte in der Quellrunde, voll übernommen";

    private bool IstHalbiert => QuellPunkte != Punkte;
}

/// <summary>Reine Funktion: Vortrag aus den Punkten der Quellrunde und dem Modus der Zielrunde (#355).</summary>
public static class PunktevortragBerechnung
{
    public static Vortragswert Berechnen(int quellPunkte, Punktevortrag modus) => modus switch
    {
        Punktevortrag.Voll                => new Vortragswert(quellPunkte, quellPunkte, quellPunkte),
        Punktevortrag.HalbiertAbgerundet  => new Vortragswert(quellPunkte / 2, quellPunkte / 2m, quellPunkte),
        Punktevortrag.HalbiertAufgerundet => new Vortragswert((quellPunkte + 1) / 2, quellPunkte / 2m, quellPunkte),
        _                                 => Vortragswert.Keiner
    };
}

/// <summary>Was eine Runde mit Punktevortrag aus ihrer Quellrunde übernimmt (#355): der Vortrag und der Platz in der
/// Abschlusstabelle der Quellrunde je Team.</summary>
public sealed record PunktevortragDaten(
    IReadOnlyDictionary<int, Vortragswert> Vortraege, IReadOnlyDictionary<int, int> Plaetze);
