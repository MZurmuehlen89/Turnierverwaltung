namespace Fussballturnierverwaltung.Domain.Enums;

public enum RankingRegelArt
{
    Punkte,
    Tordifferenz,
    ErzielteTore,
    WenigerGegentore,
    Siege,
    DirekterVergleichPunkte,
    DirekterVergleichTordifferenz,
    DirekterVergleichErzielteTore,
    DirekterVergleichAuswaertstore,
    FairplayWertung,
    Losentscheid,

    // Issue #353: Kriterien der UEFA-Ligaphase (ab 2024/25) — Summen über alle bisherigen Gegner. Wird als Zahl
    // gespeichert, neue Werte daher nur am Ende anhängen.
    GegnerPunkte,
    GegnerTordifferenz,
    GegnerErzielteTore,

    // Issue #354: Auswärtskriterien über alle Spiele (nicht nur im direkten Vergleich)
    AuswaertsToreGesamt,
    AuswaertsSiege
}
