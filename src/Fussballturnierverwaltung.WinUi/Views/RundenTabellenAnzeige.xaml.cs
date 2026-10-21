using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class RundenTabellenAnzeige : UserControl
{
    public static readonly DependencyProperty GruppeAngeklicktBefehlProperty = DependencyProperty.Register(
        nameof(GruppeAngeklicktBefehl), typeof(ICommand), typeof(RundenTabellenAnzeige), new PropertyMetadata(null));

    /// <summary>Klick auf eine Gruppenkarte (Parameter: die <c>GruppenTabelle</c>).</summary>
    public ICommand? GruppeAngeklicktBefehl
    {
        get => (ICommand?)GetValue(GruppeAngeklicktBefehlProperty);
        set => SetValue(GruppeAngeklicktBefehlProperty, value);
    }

    public RundenTabellenAnzeige()
    {
        InitializeComponent();
    }
}
