namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Hält die Reaktion auf eine Auswahländerung an, solange das ViewModel die Auswahl selbst setzt oder
/// die Liste dahinter neu aufbaut: <c>using (_spielWechsel.Betreten()) { … }</c>, die Reaktion prüft
/// <see cref="IstAktiv"/> und kehrt zurück. Ohne das löst jede eigene Zuweisung denselben Wechsel aus wie ein
/// Klick des Nutzers — samt Ungespeichert-Abfrage und einer zweiten Abfrage auf demselben DbContext
/// ("second operation"-Exception, #317). Zählt, statt nur ein Kennzeichen zu setzen, weil die Abschnitte
/// ineinander liegen: der innere darf die Unterdrückung des äußeren nicht aufheben.</summary>
public sealed class WechselUnterdrueckung
{
    private int _tiefe;

    public bool IstAktiv => _tiefe > 0;

    public IDisposable Betreten()
    {
        _tiefe++;
        return new Abschnitt(this);
    }

    private sealed class Abschnitt(WechselUnterdrueckung unterdrueckung) : IDisposable
    {
        private bool _verlassen;

        public void Dispose()
        {
            if (_verlassen) return;
            _verlassen = true;
            unterdrueckung._tiefe--;
        }
    }
}
