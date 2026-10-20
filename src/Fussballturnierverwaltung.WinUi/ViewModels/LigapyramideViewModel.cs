using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Ranglisten;
using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Aufklappbarer Bereich „Ligapyramide“ des Ranglisten-Editors (#393, #477): Profigrenze und Abschnitte.
/// Meldet Änderungen über <see cref="Geaendert"/>; der Editor entscheidet über den Dirty-Zustand.</summary>
public partial class LigapyramideViewModel : ObservableObject
{
    private int _laedtGerade;

    public event EventHandler? Geaendert;

    // Profigrenze (Spielklasse) der Ligapyramide für „Heimrecht für Unterklassige“ (#393). NumberBox liefert double,
    // leer = NaN = keine Grenze (strikt Klasse gegen Klasse).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Kopf))]
    private double _profigrenze = double.NaN;

    // Abschnitte nur zur Ansicht — gepflegt werden sie mit dem neuen Ranglisten-Editor (#477), bis dahin per Seed.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatAbschnitte))]
    [NotifyPropertyChangedFor(nameof(Kopf))]
    private IReadOnlyList<AbschnittsZeile> _abschnitte = [];

    public bool HatAbschnitte => Abschnitte.Count > 0;

    // Zugeklappt bei jedem Ranglistenwechsel, die Eintragsliste ist die Hauptfläche; die Kopfzeile fasst zusammen,
    // was festgelegt ist.
    [ObservableProperty] private bool _offen;

    public AbschnittKopf Kopf => AbschnittKopf.Aus("LIGAPYRAMIDE",
    [
        HatAbschnitte ? $"{Abschnitte.Count} Abschnitte" : null,
        double.IsNaN(Profigrenze) ? null : $"Profigrenze Spielklasse {(int)Math.Round(Profigrenze)}"
    ], "keine Abschnitte");

    /// <summary>Stand einer Rangliste übernehmen (leer bei neuer bzw. keiner Rangliste), zugeklappt.</summary>
    public void Laden(Rangliste? rangliste)
    {
        _laedtGerade++;
        try
        {
            Profigrenze = rangliste?.ProfigrenzeSpielklasse ?? double.NaN;
            Abschnitte = rangliste is null ? [] : AbschnittsUebersicht.Fuer(rangliste);
            Offen = false;
        }
        finally
        {
            _laedtGerade--;
        }
    }

    partial void OnProfigrenzeChanged(double value)
    {
        if (_laedtGerade == 0) Geaendert?.Invoke(this, EventArgs.Empty);
    }

    public int? ProfigrenzeOderNull() => double.IsNaN(Profigrenze) ? null : (int)Math.Round(Profigrenze);
}
