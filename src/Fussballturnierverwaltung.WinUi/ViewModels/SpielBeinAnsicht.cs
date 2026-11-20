using Fussballturnierverwaltung.Application.Simulation;

namespace Fussballturnierverwaltung.WinUi.ViewModels;


/// <summary>Issue #341/#247: Anzeige eines Spiels mit eigener Uhr (Hinspiel, Rückspiel, Spiel, Wiederholungsspiel) –
/// Spielkopf plus Zeitleiste in einem Objekt, das <c>SpielberichtKarte</c> direkt bindet (jede neue Simulation
/// liefert eine neue Instanz).</summary>
public sealed record SpielBeinAnsicht(SpielKopf Kopf, IReadOnlyCollection<EinzelspielEreignisZeile> Ereignisse)
{
    public bool HatEreignisse => Ereignisse.Count > 0;

    /// <summary>Die Karte des Wiederholungsspiels zu einem Spiel – <c>null</c>, wenn es keines gab. <paramref name="heim"/>/
    /// <paramref name="auswaerts"/> sind die Seiten des Spiels, dessen Verfahren zum Wiederholungsspiel führte;
    /// <paramref name="neutralerSpielort"/> entscheidet über den Heimrecht-Tausch (#247).</summary>
    public static SpielBeinAnsicht? Wiederholungsspiel(
        Wiederholungsergebnis? wiederholung, IEnumerable<ZeitleistenEreignis> ereignisse,
        SpielSeite heim, SpielSeite auswaerts, bool neutralerSpielort, UntertitelQuellen untertitel)
    {
        if (wiederholung is null) return null;

        bool getauscht = SimulationsEngine.HeimrechtTauscht(neutralerSpielort);
        return new SpielBeinAnsicht(
            SpielKopf.Wiederholungsspiel(heim, auswaerts, wiederholung, getauscht, untertitel),
            Zeitleiste.ErstellenWiederholungsspiel(wiederholung, ereignisse, heim, auswaerts, getauscht));
    }
}
