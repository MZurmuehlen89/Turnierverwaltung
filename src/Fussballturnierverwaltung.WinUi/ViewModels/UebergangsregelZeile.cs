using CommunityToolkit.Mvvm.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Eine Zeile innerhalb eines <see cref="UebergangsregelBlock"/> — je Gruppe (Gruppenphase-
/// Quellrunde), je Spiel (K.o.-Quellrunde) oder eine einzelne Zeile (Liga-Quellrunde, <see cref="GruppeIndex"/>
/// = null). <see cref="AnzahlBeste"/> (gruppenübergreifende Rangfolge-Regel, Issue #217, z.B. "2 beste
/// Gruppendritte") ist gesetzt, wenn <see cref="GruppeIndex"/> null bleibt UND die Zeile über
/// <see cref="UebergangsregelEditorViewModel.NeueGruppenuebergreifendeZeileCommand"/> angelegt wurde.</summary>
public partial class UebergangsregelZeile : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstSieger))]
    [NotifyPropertyChangedFor(nameof(IstVerlierer))]
    private int? _gruppeIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstSieger))]
    [NotifyPropertyChangedFor(nameof(IstVerlierer))]
    private int _platzierungVon = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstSieger))]
    [NotifyPropertyChangedFor(nameof(IstVerlierer))]
    private int _platzierungBis = 1;

    [ObservableProperty] private int? _anzahlBeste;

    public string GruppenLabel => AnzahlBeste is { } n
        ? $"Beste {n}"
        : GruppeIndex is null ? "—" : $"Gruppe {(char)('A' + GruppeIndex.Value - 1)}";

    // K.o.-Quellrunde: pro Spiel kann immer nur Sieger ODER Verlierer weiterziehen, daher exklusiv
    // (radiobutton-artig) statt unabhängig — Abwahl ohne Gegenauswahl wird ignoriert (Checkbox springt
    // zurück), analog WPF-Original RundenEditorViewModel.UebergangsregelZeile.
    public bool IstSieger
    {
        get => PlatzierungVon == 1 && PlatzierungBis == 1;
        set { if (value) PlatzSetzen(sieger: true); else OnPropertyChanged(nameof(IstSieger)); }
    }

    public bool IstVerlierer
    {
        get => PlatzierungVon == 2 && PlatzierungBis == 2;
        set { if (value) PlatzSetzen(sieger: false); else OnPropertyChanged(nameof(IstVerlierer)); }
    }

    /// <summary>Nur für gruppenübergreifende Zeilen (<see cref="AnzahlBeste"/> gesetzt) relevant: eine
    /// einzelne Platzierung statt eines Von/Bis-Bereichs (z.B. "Platz 3") — setzt beide Felder synchron.</summary>
    public int Platzierung
    {
        get => PlatzierungVon;
        set { PlatzierungVon = value; PlatzierungBis = value; }
    }

    private void PlatzSetzen(bool sieger)
    {
        PlatzierungVon = sieger ? 1 : 2;
        PlatzierungBis = sieger ? 1 : 2;
    }
}
