using Fussballturnierverwaltung.WinUi.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Fussballturnierverwaltung.WinUi.Views;

public sealed partial class TurnierVorlagePage : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(TurnierVorlageViewModel), typeof(TurnierVorlagePage), new PropertyMetadata(null));

    public TurnierVorlageViewModel ViewModel
    {
        get => (TurnierVorlageViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public TurnierVorlagePage() => InitializeComponent();
}
