using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

public static class VerbaendeSeedDaten
{
    public static List<Verband> Erstellen() =>
    [
        new() { Name = "FIFA",      Kurzname = "FIFA"     },
        new() { Name = "UEFA",      Kurzname = "UEFA"     },
        new() { Name = "CONMEBOL",  Kurzname = "CONMEBOL" },
        new() { Name = "CONCACAF",  Kurzname = "CONCACAF" },
        new() { Name = "CAF",       Kurzname = "CAF"      },
        new() { Name = "AFC",       Kurzname = "AFC"      },
        new() { Name = "OFC",       Kurzname = "OFC"      },
    ];
}
