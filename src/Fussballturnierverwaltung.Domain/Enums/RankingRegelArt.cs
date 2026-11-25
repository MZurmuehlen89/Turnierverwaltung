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
    AuswaertsSiege,

    // Issue #354: Platz in der Standard-Rangliste der Runde (UEFA-Klub-Koeffizient, FIFA-Weltrangliste)
    Koeffizient,

    // Issue #355: Platz in der Abschlusstabelle der Quellrunde, aus der die Runde ihre Punkte vorträgt (Belgien:
    // punktgleiche Teams der Play-offs nach ihrem Platz im Grunddurchgang). Ohne Vortragsquelle wirkungslos.
    PlatzVorrunde
}
