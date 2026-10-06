using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

public static class RegelwerkeSeedDaten
{
    // --- RankingRegeln ---

    public static List<RankingRegel> RankingRegelnErstellen() =>
    [
        new() { Name = "Punkte",                             Beschreibung = "Gesammelte Punkte",                      Regelart = RankingRegelArt.Punkte,                          Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Name = "Tordifferenz",                       Beschreibung = "Differenz Tore erzielt / Tore kassiert", Regelart = RankingRegelArt.Tordifferenz,                    Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Name = "Erzielte Tore",                      Beschreibung = "Anzahl erzielter Tore",                  Regelart = RankingRegelArt.ErzielteTore,                    Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Name = "Weniger Gegentore",                  Beschreibung = "Weniger Gegentore kassiert",             Regelart = RankingRegelArt.WenigerGegentore,                Sortierrichtung = Sortierrichtung.Aufsteigend },
        new() { Name = "Siege",                              Beschreibung = "Anzahl Siege",                           Regelart = RankingRegelArt.Siege,                           Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Name = "Direkter Vergleich (Punkte)",        Beschreibung = "Punkte aus direkten Duellen",            Regelart = RankingRegelArt.DirekterVergleichPunkte,         Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Name = "Direkter Vergleich (Tordifferenz)",  Beschreibung = "Tordiff. aus direkten Duellen",          Regelart = RankingRegelArt.DirekterVergleichTordifferenz,   Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Name = "Direkter Vergleich (Tore)",          Beschreibung = "Erzielte Tore in direkten Duellen",      Regelart = RankingRegelArt.DirekterVergleichErzielteTore,   Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Name = "Direkter Vergleich (Auswärtstore)",  Beschreibung = "Auswärtstore in direkten Duellen",       Regelart = RankingRegelArt.DirekterVergleichAuswaertstore,  Sortierrichtung = Sortierrichtung.Absteigend },
        // Der Fairplay-Wert ist negativ (-1 je Gelbe, -3 je Rote Karte) - der höhere Wert ist der bessere. Stand bis
        // #353 fälschlich auf Aufsteigend; bestehende Datenbanken: Migration FairplaySortierrichtungKorrigieren.
        new() { Name = "Fairplay-Wertung",                   Beschreibung = "Weniger Karten (Fairplay-Index)",        Regelart = RankingRegelArt.FairplayWertung,                 Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Name = "Losentscheid",                       Beschreibung = "Entscheidung per Los",                   Regelart = RankingRegelArt.Losentscheid,                    Sortierrichtung = Sortierrichtung.Aufsteigend },
        // Issue #353: Kriterien der UEFA-Ligaphase (ab 2024/25)
        new() { Name = "Punkte der Gegner",                  Beschreibung = "Summe der Punkte aller Gegner",          Regelart = RankingRegelArt.GegnerPunkte,                    Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Name = "Tordifferenz der Gegner",            Beschreibung = "Summe der Tordifferenz aller Gegner",    Regelart = RankingRegelArt.GegnerTordifferenz,              Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Name = "Tore der Gegner",                    Beschreibung = "Summe der erzielten Tore aller Gegner",  Regelart = RankingRegelArt.GegnerErzielteTore,              Sortierrichtung = Sortierrichtung.Absteigend },
        // Issue #354: Auswärtskriterien über alle Spiele
        new() { Name = "Auswärtstore",                       Beschreibung = "Anzahl erzielter Auswärtstore",          Regelart = RankingRegelArt.AuswaertsToreGesamt,             Sortierrichtung = Sortierrichtung.Absteigend },
        new() { Name = "Auswärtssiege",                      Beschreibung = "Anzahl Auswärtssiege",                   Regelart = RankingRegelArt.AuswaertsSiege,                  Sortierrichtung = Sortierrichtung.Absteigend },
    ];

    // --- RankingRegelSets ---

    public const string UEFA_LIGAPHASE_SET = "UEFA Ligaphase (ab 2024/25)";

    public static (List<RankingRegelSet> Sets, List<(string SetName, int Reihenfolge, RankingRegelArt Regelart)> Eintraege)
        RankingRegelSetsErstellen()
    {
        var sets = new List<RankingRegelSet>
        {
            new() { Name = "FIFA Standard",  Beschreibung = "Standard FIFA-Gruppenranking (seit 1994)" },
            new() { Name = "UEFA Standard",  Beschreibung = "Standard UEFA-Gruppenranking" },
            new() { Name = UEFA_LIGAPHASE_SET, Beschreibung = "Ligaphase der UEFA-Klubwettbewerbe: kein direkter Vergleich, stattdessen die Werte der Gegner" },
        };

        // Eintraege als (SetName, Reihenfolge, RegelArt) – IDs werden nach SaveChanges aufgelöst
        var eintraege = new List<(string SetName, int Reihenfolge, RankingRegelArt Regelart)>
        {
            // FIFA Standard
            ("FIFA Standard", 1, RankingRegelArt.Punkte),
            ("FIFA Standard", 2, RankingRegelArt.Tordifferenz),
            ("FIFA Standard", 3, RankingRegelArt.ErzielteTore),
            ("FIFA Standard", 4, RankingRegelArt.DirekterVergleichPunkte),
            ("FIFA Standard", 5, RankingRegelArt.DirekterVergleichTordifferenz),
            ("FIFA Standard", 6, RankingRegelArt.DirekterVergleichErzielteTore),
            ("FIFA Standard", 7, RankingRegelArt.DirekterVergleichAuswaertstore),
            ("FIFA Standard", 8, RankingRegelArt.FairplayWertung),
            ("FIFA Standard", 9, RankingRegelArt.Losentscheid),

            // UEFA Standard
            ("UEFA Standard", 1, RankingRegelArt.Punkte),
            ("UEFA Standard", 2, RankingRegelArt.DirekterVergleichPunkte),
            ("UEFA Standard", 3, RankingRegelArt.DirekterVergleichTordifferenz),
            ("UEFA Standard", 4, RankingRegelArt.DirekterVergleichErzielteTore),
            ("UEFA Standard", 5, RankingRegelArt.DirekterVergleichAuswaertstore),
            ("UEFA Standard", 6, RankingRegelArt.Tordifferenz),
            ("UEFA Standard", 7, RankingRegelArt.ErzielteTore),
            ("UEFA Standard", 8, RankingRegelArt.FairplayWertung),
            ("UEFA Standard", 9, RankingRegelArt.Losentscheid),

            // UEFA Ligaphase (Issue #353, Reihenfolge laut Reglement der UEFA-Klubwettbewerbe ab 2024/25). Der
            // Klub-Koeffizient als letztes Kriterium ist bis #354 durch den Losentscheid vertreten.
            (UEFA_LIGAPHASE_SET, 1, RankingRegelArt.Punkte),
            (UEFA_LIGAPHASE_SET, 2, RankingRegelArt.Tordifferenz),
            (UEFA_LIGAPHASE_SET, 3, RankingRegelArt.ErzielteTore),
            (UEFA_LIGAPHASE_SET, 4, RankingRegelArt.AuswaertsToreGesamt),
            (UEFA_LIGAPHASE_SET, 5, RankingRegelArt.Siege),
            (UEFA_LIGAPHASE_SET, 6, RankingRegelArt.AuswaertsSiege),
            (UEFA_LIGAPHASE_SET, 7, RankingRegelArt.GegnerPunkte),
            (UEFA_LIGAPHASE_SET, 8, RankingRegelArt.GegnerTordifferenz),
            (UEFA_LIGAPHASE_SET, 9, RankingRegelArt.GegnerErzielteTore),
            (UEFA_LIGAPHASE_SET, 10, RankingRegelArt.FairplayWertung),
            (UEFA_LIGAPHASE_SET, 11, RankingRegelArt.Losentscheid),
        };

        return (sets, eintraege);
    }

    // --- Regelwerke ---

    public static List<(Regelwerk Regelwerk, int Sieg, int Unentschieden, int Niederlage, VerfahrenArt[] Verfahren)>
        RegelwerkeErstellen(IReadOnlyDictionary<string, int> rankingRegelSetLookup) =>
    [
        (
            new Regelwerk
            {
                Name = "FIFA Standard Modern (3-Punkte)",
                Beschreibung = "Aktuelles FIFA-Regelwerk: 3 Punkte für Sieg, Verlängerung + Elfmeter im K.-o.",
                RankingRegelSetId = rankingRegelSetLookup.GetValueOrDefault("FIFA Standard")
            },
            Sieg: 3, Unentschieden: 1, Niederlage: 0,
            Verfahren: [VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen]
        ),
        (
            new Regelwerk
            {
                Name = "FIFA Historisch (2-Punkte, vor 1994)",
                Beschreibung = "Regelwerk vor 1994: 2 Punkte für Sieg.",
                GueltigBis = new DateOnly(1994, 6, 30),
                RankingRegelSetId = rankingRegelSetLookup.GetValueOrDefault("FIFA Standard")
            },
            Sieg: 2, Unentschieden: 1, Niederlage: 0,
            Verfahren: [VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen]
        ),
        (
            new Regelwerk
            {
                Name = "UEFA EM 1996 (Golden Goal)",
                Beschreibung = "EM 1996 Regelwerk: Golden Goal in der Verlängerung.",
                GueltigVon = new DateOnly(1996, 1, 1),
                GueltigBis = new DateOnly(1996, 12, 31),
                RankingRegelSetId = rankingRegelSetLookup.GetValueOrDefault("UEFA Standard")
            },
            Sieg: 3, Unentschieden: 1, Niederlage: 0,
            Verfahren: [VerfahrenArt.GoldenGoal, VerfahrenArt.Elfmeterschiessen]
        ),
        (
            new Regelwerk
            {
                Name = "UEFA EM 2004 (Silver Goal)",
                Beschreibung = "EM 2004 Regelwerk: Silver Goal – Halbzeit der Verlängerung entscheidet.",
                GueltigVon = new DateOnly(2004, 1, 1),
                GueltigBis = new DateOnly(2004, 12, 31),
                RankingRegelSetId = rankingRegelSetLookup.GetValueOrDefault("UEFA Standard")
            },
            Sieg: 3, Unentschieden: 1, Niederlage: 0,
            Verfahren: [VerfahrenArt.SilverGoal, VerfahrenArt.Elfmeterschiessen]
        ),
        (
            new Regelwerk
            {
                Name = "Historisch: Wiederholungsspiel + Los",
                Beschreibung = "Älteres Regelwerk: Bei Gleichstand Wiederholungsspiel, dann Losentscheid.",
                GueltigBis = new DateOnly(1970, 12, 31)
            },
            Sieg: 2, Unentschieden: 1, Niederlage: 0,
            Verfahren: [VerfahrenArt.Wiederholungsspiel, VerfahrenArt.Losentscheid]
        ),
        (
            new Regelwerk
            {
                Name = "UEFA Clubwettbewerb Standard",
                Beschreibung = "UEFA-Regelwerk für Clubwettbewerbe: 3 Punkte, UEFA-Gruppenranking (direkter Vergleich zuerst), Verlängerung + Elfmeter.",
                GueltigVon = new DateOnly(2021, 1, 1),
                RankingRegelSetId = rankingRegelSetLookup.GetValueOrDefault("UEFA Standard")
            },
            Sieg: 3, Unentschieden: 1, Niederlage: 0,
            Verfahren: [VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen]
        ),
        // Issue #353: eigenes Regelwerk für CL/EL/ECL ab 2024/25 — "UEFA Clubwettbewerb Standard" bleibt für die
        // Formate mit Gruppenphase (direkter Vergleich zuerst) bestehen.
        (
            new Regelwerk
            {
                Name = TurnierVorlagenSeedDaten.UEFA_LIGAPHASE,
                Beschreibung = "UEFA-Regelwerk für Clubwettbewerbe ab 2024/25: 3 Punkte, Rangfolge der Ligaphase ohne direkten Vergleich (Tordifferenz, Tore, Auswärtstore, Siege, Auswärtssiege, dann die Werte der Gegner), Verlängerung + Elfmeter.",
                GueltigVon = new DateOnly(2024, 7, 1),
                RankingRegelSetId = rankingRegelSetLookup.GetValueOrDefault(UEFA_LIGAPHASE_SET)
            },
            Sieg: 3, Unentschieden: 1, Niederlage: 0,
            Verfahren: [VerfahrenArt.VerlaengerungKlassisch, VerfahrenArt.Elfmeterschiessen]
        ),
        // Issue #350: nationale Ligen brauchen kein Entscheidungsverfahren (kein K.-o.), unterscheiden sich
        // aber im Tiebreaker — Bundesliga/Premier League/Ligue 1 werten die Tordifferenz vor dem direkten
        // Vergleich, La Liga/Serie A umgekehrt.
        (
            new Regelwerk
            {
                Name = TurnierVorlagenSeedDaten.LIGA_TORDIFFERENZ,
                Beschreibung = "Nationale Liga: 3 Punkte für Sieg, Tordifferenz vor direktem Vergleich (z. B. Bundesliga, Premier League, Ligue 1).",
                RankingRegelSetId = rankingRegelSetLookup.GetValueOrDefault("FIFA Standard")
            },
            Sieg: 3, Unentschieden: 1, Niederlage: 0,
            Verfahren: []
        ),
        (
            new Regelwerk
            {
                Name = TurnierVorlagenSeedDaten.LIGA_DIREKTER_VERGLEICH,
                Beschreibung = "Nationale Liga: 3 Punkte für Sieg, direkter Vergleich vor Tordifferenz (z. B. La Liga, Serie A).",
                RankingRegelSetId = rankingRegelSetLookup.GetValueOrDefault("UEFA Standard")
            },
            Sieg: 3, Unentschieden: 1, Niederlage: 0,
            Verfahren: []
        ),
        // Issue #374: Copa América ohne Verlängerung — in Viertelfinale, Halbfinale und Spiel um Platz 3 seit 2015
        // (2019 nur im Viertelfinale), bis 2007 auch im Finale. Rangfolge der Gruppen wie im FIFA-Regelwerk.
        (
            new Regelwerk
            {
                Name = TurnierVorlagenSeedDaten.COPA_DIREKTES_ELFMETERSCHIESSEN,
                Beschreibung = "Copa-América-Regelwerk für K.-o.-Spiele ohne Verlängerung: 3 Punkte für Sieg, bei Unentschieden direkt Elfmeterschießen.",
                RankingRegelSetId = rankingRegelSetLookup.GetValueOrDefault("FIFA Standard")
            },
            Sieg: 3, Unentschieden: 1, Niederlage: 0,
            Verfahren: [VerfahrenArt.Elfmeterschiessen]
        ),
    ];
}
