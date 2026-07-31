using Fluent;
using Fussballturnierverwaltung.Wpf.Ribbon;
using Fussballturnierverwaltung.Wpf.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Fussballturnierverwaltung.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly List<RibbonGroupBox> _kontextGruppenBoxen = [];

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        AktualisiereKontextGruppen();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HatRibbonBereich) && _viewModel.HatRibbonBereich)
            SeitenTab.IsSelected = true;

        if (e.PropertyName == nameof(MainViewModel.RibbonZusatzGruppen))
            AktualisiereKontextGruppen();
    }

    // Editor-spezifische Ribbon-Gruppen können nicht per ItemsSource gebunden werden,
    // da SeitenTab bereits die beiden statischen Gruppen "Datensatz"/"Bearbeiten" als
    // XAML-Kinder besitzt (ItemsSource und deklarative Items schließen sich in einem
    // ItemsControl gegenseitig aus). Stattdessen werden die Zusatzgruppen direkt in
    // SeitenTab.Groups verwaltet.
    private void AktualisiereKontextGruppen()
    {
        foreach (var alteGruppe in _kontextGruppenBoxen)
            SeitenTab.Groups.Remove(alteGruppe);
        _kontextGruppenBoxen.Clear();

        foreach (var gruppe in _viewModel.RibbonZusatzGruppen)
        {
            var groupBox = new RibbonGroupBox { Header = gruppe.Header };
            foreach (var aktion in gruppe.Aktionen)
                groupBox.Items.Add(RibbonButtonErstellen(aktion));

            SeitenTab.Groups.Add(groupBox);
            _kontextGruppenBoxen.Add(groupBox);
        }
    }

    private static Fluent.Button RibbonButtonErstellen(RibbonAktion aktion)
    {
        var button = new Fluent.Button
        {
            Header = aktion.Header,
            Command = aktion.Befehl,
            SizeDefinition = new RibbonControlSizeDefinition("Large"),
        };

        if (aktion.IconGlyph is not null)
        {
            button.LargeIcon = new TextBlock
            {
                Text = aktion.IconGlyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 32,
            };
            button.Icon = new TextBlock
            {
                Text = aktion.IconGlyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
            };
        }

        return button;
    }

    private void BeendenButton_Click(object sender, RoutedEventArgs e)
        => Close();
}
