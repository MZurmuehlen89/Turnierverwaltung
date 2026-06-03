using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Infrastructure.Seed.Daten;

public static class LaenderSeedDaten
{
    public static List<Land> Erstellen(IReadOnlyDictionary<string, int> verbaendeLookup)
    {
        var uefa     = verbaendeLookup["UEFA"];
        var conmebol = verbaendeLookup["CONMEBOL"];
        var concacaf = verbaendeLookup["CONCACAF"];
        var caf      = verbaendeLookup["CAF"];
        var afc      = verbaendeLookup["AFC"];
        var ofc      = verbaendeLookup["OFC"];

        return
        [
            // --- Europa ---
            new() { Name = "Deutschland",     Kurzname = "GER", FifaCode = "GER", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Frankreich",      Kurzname = "FRA", FifaCode = "FRA", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "England",         Kurzname = "ENG", FifaCode = "ENG", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Spanien",         Kurzname = "ESP", FifaCode = "ESP", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Italien",         Kurzname = "ITA", FifaCode = "ITA", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Niederlande",     Kurzname = "NED", FifaCode = "NED", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Portugal",        Kurzname = "POR", FifaCode = "POR", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Belgien",         Kurzname = "BEL", FifaCode = "BEL", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Kroatien",        Kurzname = "CRO", FifaCode = "CRO", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Schweiz",         Kurzname = "SUI", FifaCode = "SUI", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Österreich",      Kurzname = "AUT", FifaCode = "AUT", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Dänemark",        Kurzname = "DEN", FifaCode = "DEN", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Schweden",        Kurzname = "SWE", FifaCode = "SWE", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Norwegen",        Kurzname = "NOR", FifaCode = "NOR", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Polen",           Kurzname = "POL", FifaCode = "POL", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Türkei",          Kurzname = "TUR", FifaCode = "TUR", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Ungarn",          Kurzname = "HUN", FifaCode = "HUN", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Tschechien",      Kurzname = "CZE", FifaCode = "CZE", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Serbien",         Kurzname = "SRB", FifaCode = "SRB", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Schottland",      Kurzname = "SCO", FifaCode = "SCO", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Ukraine",         Kurzname = "UKR", FifaCode = "UKR", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Rumänien",        Kurzname = "ROU", FifaCode = "ROU", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Griechenland",    Kurzname = "GRE", FifaCode = "GRE", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Slowakei",        Kurzname = "SVK", FifaCode = "SVK", Kontinent = Kontinent.Europa,              VerbandId = uefa     },
            new() { Name = "Slowenien",       Kurzname = "SVN", FifaCode = "SVN", Kontinent = Kontinent.Europa,              VerbandId = uefa     },

            // --- Südamerika ---
            new() { Name = "Brasilien",       Kurzname = "BRA", FifaCode = "BRA", Kontinent = Kontinent.Suedamerika,         VerbandId = conmebol },
            new() { Name = "Argentinien",     Kurzname = "ARG", FifaCode = "ARG", Kontinent = Kontinent.Suedamerika,         VerbandId = conmebol },
            new() { Name = "Uruguay",         Kurzname = "URU", FifaCode = "URU", Kontinent = Kontinent.Suedamerika,         VerbandId = conmebol },
            new() { Name = "Kolumbien",       Kurzname = "COL", FifaCode = "COL", Kontinent = Kontinent.Suedamerika,         VerbandId = conmebol },
            new() { Name = "Chile",           Kurzname = "CHI", FifaCode = "CHI", Kontinent = Kontinent.Suedamerika,         VerbandId = conmebol },
            new() { Name = "Ecuador",         Kurzname = "ECU", FifaCode = "ECU", Kontinent = Kontinent.Suedamerika,         VerbandId = conmebol },
            new() { Name = "Peru",            Kurzname = "PER", FifaCode = "PER", Kontinent = Kontinent.Suedamerika,         VerbandId = conmebol },
            new() { Name = "Paraguay",        Kurzname = "PAR", FifaCode = "PAR", Kontinent = Kontinent.Suedamerika,         VerbandId = conmebol },
            new() { Name = "Venezuela",       Kurzname = "VEN", FifaCode = "VEN", Kontinent = Kontinent.Suedamerika,         VerbandId = conmebol },
            new() { Name = "Bolivien",        Kurzname = "BOL", FifaCode = "BOL", Kontinent = Kontinent.Suedamerika,         VerbandId = conmebol },

            // --- Nord- und Mittelamerika ---
            new() { Name = "USA",             Kurzname = "USA", FifaCode = "USA", Kontinent = Kontinent.NordUndMittelamerika, VerbandId = concacaf },
            new() { Name = "Mexiko",          Kurzname = "MEX", FifaCode = "MEX", Kontinent = Kontinent.NordUndMittelamerika, VerbandId = concacaf },
            new() { Name = "Kanada",          Kurzname = "CAN", FifaCode = "CAN", Kontinent = Kontinent.NordUndMittelamerika, VerbandId = concacaf },
            new() { Name = "Costa Rica",      Kurzname = "CRC", FifaCode = "CRC", Kontinent = Kontinent.NordUndMittelamerika, VerbandId = concacaf },
            new() { Name = "Jamaika",         Kurzname = "JAM", FifaCode = "JAM", Kontinent = Kontinent.NordUndMittelamerika, VerbandId = concacaf },

            // --- Afrika ---
            new() { Name = "Senegal",         Kurzname = "SEN", FifaCode = "SEN", Kontinent = Kontinent.Afrika,              VerbandId = caf      },
            new() { Name = "Marokko",         Kurzname = "MAR", FifaCode = "MAR", Kontinent = Kontinent.Afrika,              VerbandId = caf      },
            new() { Name = "Nigeria",         Kurzname = "NGA", FifaCode = "NGA", Kontinent = Kontinent.Afrika,              VerbandId = caf      },
            new() { Name = "Kamerun",         Kurzname = "CMR", FifaCode = "CMR", Kontinent = Kontinent.Afrika,              VerbandId = caf      },
            new() { Name = "Ghana",           Kurzname = "GHA", FifaCode = "GHA", Kontinent = Kontinent.Afrika,              VerbandId = caf      },
            new() { Name = "Ägypten",         Kurzname = "EGY", FifaCode = "EGY", Kontinent = Kontinent.Afrika,              VerbandId = caf      },
            new() { Name = "Elfenbeinküste",  Kurzname = "CIV", FifaCode = "CIV", Kontinent = Kontinent.Afrika,              VerbandId = caf      },
            new() { Name = "Algerien",        Kurzname = "ALG", FifaCode = "ALG", Kontinent = Kontinent.Afrika,              VerbandId = caf      },
            new() { Name = "Tunesien",        Kurzname = "TUN", FifaCode = "TUN", Kontinent = Kontinent.Afrika,              VerbandId = caf      },
            new() { Name = "Südafrika",       Kurzname = "RSA", FifaCode = "RSA", Kontinent = Kontinent.Afrika,              VerbandId = caf      },
            new() { Name = "Mali",            Kurzname = "MLI", FifaCode = "MLI", Kontinent = Kontinent.Afrika,              VerbandId = caf      },

            // --- Asien ---
            new() { Name = "Japan",           Kurzname = "JPN", FifaCode = "JPN", Kontinent = Kontinent.Asien,               VerbandId = afc      },
            new() { Name = "Südkorea",        Kurzname = "KOR", FifaCode = "KOR", Kontinent = Kontinent.Asien,               VerbandId = afc      },
            new() { Name = "Saudi-Arabien",   Kurzname = "KSA", FifaCode = "KSA", Kontinent = Kontinent.Asien,               VerbandId = afc      },
            new() { Name = "Iran",            Kurzname = "IRN", FifaCode = "IRN", Kontinent = Kontinent.Asien,               VerbandId = afc      },
            new() { Name = "Australien",      Kurzname = "AUS", FifaCode = "AUS", Kontinent = Kontinent.Asien,               VerbandId = afc      },
            new() { Name = "Katar",           Kurzname = "QAT", FifaCode = "QAT", Kontinent = Kontinent.Asien,               VerbandId = afc      },
            new() { Name = "China",           Kurzname = "CHN", FifaCode = "CHN", Kontinent = Kontinent.Asien,               VerbandId = afc      },

            // --- Ozeanien ---
            new() { Name = "Neuseeland",      Kurzname = "NZL", FifaCode = "NZL", Kontinent = Kontinent.Ozeanien,            VerbandId = ofc      },
        ];
    }
}
