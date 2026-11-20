using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

/// <summary>Vereinheitlichte Spielbericht-Karte mit Leer-Zustand (#302, #247). <see cref="LeerZustandText"/> ist der
/// einzige View-spezifische Teil — der DataContext liefert lose über Property-Namen <c>Protokoll</c>
/// (<see cref="ViewModels.SpielBeinAnsicht"/>), <c>HatProtokoll</c> und <c>ZeigeLeerZustand</c>, die
/// <see cref="ViewModels.SimulationsViewModel"/> und <see cref="ViewModels.EinzelspielSimulationsViewModel"/> bereitstellen.</summary>
public sealed partial class SpielberichtControl : UserControl
{
    public static readonly DependencyProperty LeerZustandTextProperty = DependencyProperty.Register(
        nameof(LeerZustandText), typeof(string), typeof(SpielberichtControl), new PropertyMetadata(null));

    public string? LeerZustandText
    {
        get => (string?)GetValue(LeerZustandTextProperty);
        set => SetValue(LeerZustandTextProperty, value);
    }

    public SpielberichtControl()
    {
        InitializeComponent();
    }
}
