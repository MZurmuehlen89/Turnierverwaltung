using Fussballturnierverwaltung.Application.Simulation;

namespace Fussballturnierverwaltung.WinUi.ViewModels;


/// <summary>Issue #341: Kopf einer Hinspiel-/Rückspiel-Karte (Expander-Header) — Spielkopf plus
/// kompakter Zeitstrahl in einem Objekt, damit der Header über den bewährten Header/HeaderTemplate-Weg
/// gebunden bleibt (jede neue Simulation liefert eine neue Instanz).</summary>
public sealed record SpielBeinAnsicht(SpielKopf Kopf, IReadOnlyCollection<EinzelspielEreignisZeile> Ereignisse)
{
    public bool HatEreignisse => Ereignisse.Count > 0;
}

