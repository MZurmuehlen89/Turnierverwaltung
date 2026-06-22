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
        new() { Name = "Fairplay-Wertung",                   Beschreibung = "Weniger Karten (Fairplay-Index)",        Regelart = RankingRegelArt.FairplayWertung,                 Sortierrichtung = Sortierrichtung.Aufsteigend },
        new() { Name = "Losentscheid",                       Beschreibung = "Entscheidung per Los",                   Regelart = RankingRegelArt.Losentscheid,                    Sortierrichtung = Sortierrichtung.Aufsteigend },
    ];

    // --- RankingRegelSets ---

    public static (List<RankingRegelSet> Sets, List<(string SetName, int Reihenfolge, RankingRegelArt Regelart)> Eintraege)
        RankingRegelSetsErstellen()
    {
        var sets = new List<RankingRegelSet>
        {
            new() { Name = "FIFA Standard",  Beschreibung = "Standard FIFA-Gruppenranking (seit 1994)" },
            new() { Name = "UEFA Standard",  Beschreibung = "Standard UEFA-Gruppenranking" },
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
    ];
}
