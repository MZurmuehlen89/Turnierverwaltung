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
                groupBox.Items.Add(RibbonElementErstellen(aktion));

            SeitenTab.Groups.Add(groupBox);
            _kontextGruppenBoxen.Add(groupBox);
        }
    }

    /// <summary>Wählt je nach Kombination der <see cref="RibbonAktion"/>-Eigenschaften den passenden
    /// Fluent-Control-Typ: reiner Klick-Befehl → Button; Befehl + eigenständig ausführbare
    /// Unteraktionen (z.B. "mit TV-Modus ausführen") → SplitButton (Klick = Befehl, Pfeil = Dropdown
    /// mit direkt anwählbaren Menüpunkten, kein Umschalt-/Checkbox-Zustand); Unteraktionen ohne
    /// eigenen Befehl (reines Aktionsmenü, z.B. "Runde"-Aktionen) → DropDownButton mit denselben
    /// Menüpunkten; dynamische Item-Quelle ohne Befehl (z.B. Auslosungs-Regeln) → DropDownButton.</summary>
    private static UIElement RibbonElementErstellen(RibbonAktion aktion)
    {
        if (aktion.Unteraktionen is { Count: > 0 } && aktion.Befehl is null)
        {
            var dropDownButton = new Fluent.DropDownButton
            {
                Header = aktion.Header,
                SizeDefinition = new RibbonControlSizeDefinition("Large"),
            };
            IconSetzen(dropDownButton, aktion.IconGlyph);
            foreach (var unteraktion in aktion.Unteraktionen)
                dropDownButton.Items.Add(MenuItemErstellen(unteraktion));
            return dropDownButton;
        }

        if (aktion.Unteraktionen is { Count: > 0 })
        {
            var splitButton = new Fluent.SplitButton
            {
                Header = aktion.Header,
                Command = aktion.Befehl,
                SizeDefinition = new RibbonControlSizeDefinition("Large"),
            };
            IconSetzen(splitButton, aktion.IconGlyph);
            foreach (var unteraktion in aktion.Unteraktionen)
                splitButton.Items.Add(MenuItemErstellen(unteraktion));
            return splitButton;
        }

        if (aktion.ItemsQuelleBinding is not null)
        {
            var dropDownButton = new Fluent.DropDownButton
            {
                Header = aktion.Header,
                SizeDefinition = new RibbonControlSizeDefinition("Large"),
                MaxDropDownHeight = double.PositiveInfinity,
            };
            IconSetzen(dropDownButton, aktion.IconGlyph);
            dropDownButton.SetBinding(ItemsControl.ItemsSourceProperty, aktion.ItemsQuelleBinding);
            return dropDownButton;
        }

        var button = new Fluent.Button
        {
            Header = aktion.Header,
            Command = aktion.Befehl,
            SizeDefinition = new RibbonControlSizeDefinition("Large"),
        };
        IconSetzen(button, aktion.IconGlyph);
        return button;
    }

    private static Fluent.MenuItem MenuItemErstellen(RibbonAktion aktion)
    {
        var menuItem = new Fluent.MenuItem { Header = aktion.Header, Command = aktion.Befehl };
        if (aktion.IconGlyph is not null)
        {
            menuItem.Icon = new TextBlock
            {
                Text = aktion.IconGlyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
            };
        }
        return menuItem;
    }

    private static void IconSetzen(IRibbonControl control, string? iconGlyph)
    {
        if (iconGlyph is null) return;

        control.Icon = new TextBlock
        {
            Text = iconGlyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 16,
        };
        if (control is ILargeIconProvider largeIconProvider)
        {
            largeIconProvider.LargeIcon = new TextBlock
            {
                Text = iconGlyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 32,
            };
        }
    }

    private void BeendenButton_Click(object sender, RoutedEventArgs e)
        => Close();
}
