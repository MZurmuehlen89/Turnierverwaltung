using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Statistiken;

public enum SpielAusgang
{
    Sieg,
    Unentschieden,
    Niederlage
}

public enum SpielortFilter
{
    Alle,
    Heim,
    Auswaerts
}

/// <summary>Ein abgeschlossenes Spiel aus Sicht einer der beiden Mannschaften — die gemeinsame Grundlage aller
/// Ranglisten (#327). Der Ausgang ergibt sich aus den Toren inklusive Verlängerung, nicht aus
/// <see cref="SpielErgebnis.SiegerMannschaftId"/>: dort steht im Rückspiel einer K.-o.-Paarung der Sieger der
/// Gesamtwertung, nicht des Spiels. Ein Elfmeterschießen zählt damit als Unentschieden (reales Vorbild).</summary>
public record MannschaftsSpiel(
    Mannschaft Mannschaft,
    bool IstHeim,
    bool NeutralerSpielort,
    int Tore,
    int Gegentore,
    int? HalbzeitTore,
    int? HalbzeitGegentore,
    int GelbeKarten,
    int RoteKarten,
    PunkteRegel PunkteRegel)
{
    public SpielAusgang Ausgang => AusgangAus(Tore, Gegentore);

    public SpielAusgang? HalbzeitAusgang
        => HalbzeitTore is { } tore && HalbzeitGegentore is { } gegentore ? AusgangAus(tore, gegentore) : null;

    public int Punkte => PunkteFuer(Ausgang);

    public int PunkteFuer(SpielAusgang ausgang) => ausgang switch
    {
        SpielAusgang.Sieg          => PunkteRegel.PunkteFuerSieg,
        SpielAusgang.Unentschieden => PunkteRegel.PunkteFuerUnentschieden,
        _                          => PunkteRegel.PunkteFuerNiederlage
    };

    private static SpielAusgang AusgangAus(int tore, int gegentore)
    {
        if (tore > gegentore) return SpielAusgang.Sieg;
        if (tore < gegentore) return SpielAusgang.Niederlage;
        return SpielAusgang.Unentschieden;
    }
}

public record Bilanz(
    int Spiele,
    int Siege,
    int Unentschieden,
    int Niederlagen,
    int Tore,
    int Gegentore,
    int Punkte)
{
    public int Tordifferenz => Tore - Gegentore;
}

public record BilanzEintrag(Mannschaft Mannschaft, Bilanz Bilanz);

public record FormEintrag(Mannschaft Mannschaft, IReadOnlyList<SpielAusgang> LetzteSpiele, int Punkte);

/// <summary><paramref name="Bilanz"/> wertet den Pausenstand, als wäre das Spiel zur Halbzeit beendet.
/// Gedreht = Rückstand zur Pause, am Ende gewonnen; verspielt = Führung zur Pause, am Ende verloren.</summary>
public record HalbzeitEintrag(Mannschaft Mannschaft, Bilanz Bilanz, int Gedreht, int Verspielt);

public record Serie(int Laengste, int Aktuell);

public record SerienEintrag(Mannschaft Mannschaft, Serie Siege, Serie Ungeschlagen, Serie OhneGegentor);

public record ToreEintrag(
    Mannschaft Mannschaft,
    int Spiele,
    int Tore,
    int Gegentore,
    int ZuNullSpiele,
    int SpieleOhneEigenesTor)
{
    public double ToreProSpiel      => Spiele > 0 ? (double)Tore      / Spiele : 0;
    public double GegentoreProSpiel => Spiele > 0 ? (double)Gegentore / Spiele : 0;
}

public record FairplayEintrag(Mannschaft Mannschaft, int Spiele, int GelbeKarten, int RoteKarten)
{
    // Dieselbe Gewichtung wie die Fairplay-Wertung der Gruppentabelle (TabellenService): Gelb 1, Rot 3.
    public int Wertung => GelbeKarten + RoteKarten * 3;
}

/// <summary>Gemeinsame Bilanz aller Mannschaften eines Verbands (Nationalmannschaften) bzw. Landes (Vereine).</summary>
public record VerbandEintrag(string Name, int Mannschaften, Bilanz Bilanz);

public record DirektvergleichErgebnis(
    Mannschaft MannschaftA,
    Mannschaft MannschaftB,
    int SiegeA,
    int Unentschieden,
    int SiegeB,
    int ToreA,
    int ToreB,
    IReadOnlyList<Spiel> Spiele);
