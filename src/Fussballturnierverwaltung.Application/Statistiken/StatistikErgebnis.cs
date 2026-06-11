using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Statistiken;

public record TorStatistikEintrag(
    Mannschaft Mannschaft,
    int ToreErzielt,
    int ToreKassiert)
{
    public int Tordifferenz => ToreErzielt - ToreKassiert;
}

public record FormEintrag(
    Mannschaft Mannschaft,
    IReadOnlyList<char> LetzteErgebnisse,
    int Punkte);

public record DirektvergleichErgebnis(
    Mannschaft MannschaftA,
    Mannschaft MannschaftB,
    int SiegeA,
    int Unentschieden,
    int SiegeB,
    int ToreA,
    int ToreB,
    IReadOnlyList<Spiel> Spiele);

public record HeimAuswaertsEintrag(
    Mannschaft Mannschaft,
    int HeimSpiele, int HeimSiege, int HeimUnentschieden, int HeimNiederlagen,
    int HeimTore, int HeimGegentore,
    int AuswaertsSpiele, int AuswaertsSiege, int AuswaertsUnentschieden, int AuswaertsNiederlagen,
    int AuswaertsTore, int AuswaertsGegentore)
{
    public double HeimSiegquote      => HeimSpiele      > 0 ? (double)HeimSiege      / HeimSpiele      * 100 : 0;
    public double AuswaertsSiegquote => AuswaertsSpiele > 0 ? (double)AuswaertsSiege / AuswaertsSpiele * 100 : 0;
}
