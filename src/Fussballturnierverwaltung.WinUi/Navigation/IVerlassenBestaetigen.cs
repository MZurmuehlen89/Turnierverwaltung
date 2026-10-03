namespace Fussballturnierverwaltung.WinUi.Navigation;

/// <summary>Für ViewModels, die vor dem Wegnavigieren einen laufenden Vorgang per Rückfrage beenden lassen
/// (z.B. die TV-Ziehung der Auslosung, #406) — anders als <see cref="IHatUngespeicherteAenderungen"/> gibt es
/// dabei nichts zu speichern, nur fortfahren oder bleiben.</summary>
public interface IVerlassenBestaetigen
{
    /// <summary><c>false</c> = der Nutzer will bleiben, die Navigation unterbleibt.</summary>
    Task<bool> VerlassenBestaetigenAsync();
}
