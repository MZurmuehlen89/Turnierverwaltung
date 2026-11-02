namespace Fussballturnierverwaltung.Application.Auslosungen;

/// <summary>Die bei dieser Auslosung tatsächlich wirksamen Regeln (siehe <see cref="AuslosungsRegelKompatibilitaet"/>).</summary>
public sealed record RegelKontext(
    bool KeinGleichesLand, bool KeineGleicheGruppe, bool HeimrechtNachLos, bool HeimrechtNachSetzliste,
    bool FreilosMoeglich, bool Konfoederationsverteilung, bool MitRueckspiel,
    Spielklassen? HeimrechtFuerUnterklassige = null, bool HeimrechtFuerUnterenTopf = false)
{
    // Paarweise Regeln. Die Konföderationsverteilung zählt je Gruppe und passt nicht in dieses Schema — sie
    // wirkt nur in der Gruppenauslosung (GruppenAuslosung).
    internal bool Verbietet(TeilnehmerSlot a, TeilnehmerSlot b) =>
        PaarungsRegeln.VerletzteRegel(
            KeinGleichesLand, KeineGleicheGruppe, new(a.LandId, a.Gruppe), new(b.LandId, b.Gruppe)) is not null;

    public string EinschraenkungenText =>
        string.Join(", ", new[]
        {
            KeinGleichesLand ? "'Kein gleiches Land'" : null,
            KeineGleicheGruppe ? "'Keine gleiche Gruppe'" : null,
            Konfoederationsverteilung ? "'Konföderationsverteilung'" : null
        }.OfType<string>()) is { Length: > 0 } text ? text : "keine";
}
