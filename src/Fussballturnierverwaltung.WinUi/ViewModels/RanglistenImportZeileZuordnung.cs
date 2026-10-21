using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Auswahloption für die Zuordnungs-ComboBox einer nicht aufgelösten Zeile — Mannschaft null
/// + IstNeuAnlegen false bedeutet "Überspringen", Mannschaft null + IstNeuAnlegen true blendet das
/// Inline-Schnell-Anlegen-Formular der Zeile ein (Issue #227). Anders als im WPF-Original kein
/// separater modaler Dialog: WinUI 3 kann keinen zweiten ContentDialog über dem Import-Dialog
/// anzeigen, daher Inline-Formular direkt in der Zeile.</summary>
public record MannschaftAuswahlOption(string Anzeige, Mannschaft? Mannschaft, bool IstNeuAnlegen = false);

/// <summary>Trägt Service/Land-Liste und den Anlegen-Befehl direkt am Zeilen-Objekt statt am
/// übergeordneten ViewModel: Ein ItemTemplate einer ListView hat in WinUI 3 keinen zuverlässigen Weg
/// zurück zum DataContext des umschließenden Elements (RelativeSource kennt nur Self/TemplatedParent,
/// kein FindAncestor wie in WPF; ElementName-Bindings innerhalb von DataTemplates sind in WinUI 3
/// ebenfalls nicht zuverlässig, vgl. das analoge WPF-Problem mit DataGridComboBoxColumn ohne Visual-
/// Tree-Zugriff). Der Callback synchronisiert die neu angelegte Mannschaft als Option in alle
/// Geschwister-Zeilen (Issue #227).</summary>
public partial class UnaufgeloesteZeileZuordnung : ObservableObject
{
    private readonly MannschaftService _mannschaftService;
    private readonly Func<UnaufgeloesteZeileZuordnung, Mannschaft, Task> _mannschaftAngelegt;

    public RanglistenRohZeile Quelle { get; }
    public ObservableCollection<MannschaftAuswahlOption> Optionen { get; }
    public IReadOnlyList<Land> Laender { get; }

    [ObservableProperty] private MannschaftAuswahlOption _ausgewaehlt;

    [ObservableProperty] private bool _neuAnlegenAktiv;
    [ObservableProperty] private string _neuName;
    [ObservableProperty] private string _neuKurzname;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeuIstVerein))]
    private bool _neuIstNationalmannschaft = true;

    public bool NeuIstVerein
    {
        get => !NeuIstNationalmannschaft;
        set => NeuIstNationalmannschaft = !value;
    }

    [ObservableProperty] private Land? _neuLand;
    [ObservableProperty] private int _neuOffensiv = 60;
    [ObservableProperty] private int _neuMittelfeld = 60;
    [ObservableProperty] private int _neuDefensiv = 60;
    [ObservableProperty] private int _neuHeimstaerke = 60;
    [ObservableProperty] private int _neuAuswaertsstaerke = 60;
    [ObservableProperty] private string? _neuFehler;

    public UnaufgeloesteZeileZuordnung(
        RanglistenRohZeile quelle,
        IReadOnlyList<MannschaftAuswahlOption> optionen,
        IReadOnlyList<Land> laender,
        MannschaftService mannschaftService,
        Func<UnaufgeloesteZeileZuordnung, Mannschaft, Task> mannschaftAngelegt)
    {
        Quelle = quelle;
        Optionen = new ObservableCollection<MannschaftAuswahlOption>(optionen);
        Laender = laender;
        _mannschaftService = mannschaftService;
        _mannschaftAngelegt = mannschaftAngelegt;
        _ausgewaehlt = optionen[0];
        _neuName = quelle.RohName;
        _neuKurzname = new string(quelle.RohName.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
    }

    // Null-Guard: WinUI-ComboBox setzt SelectedItem beim Austausch der ItemsSource (z.B. erneutes
    // "Abrufen", das Unaufgeloest komplett neu befüllt) kurzzeitig auf null zurück, bevor sie den
    // passenden Eintrag wiederfindet — analog zum bestehenden Fallstrick bei
    // MannschaftenViewModel.OnFilterLandOptionChanged.
    partial void OnAusgewaehltChanged(MannschaftAuswahlOption value)
    {
        if (value is null)
        {
            Ausgewaehlt = Optionen[0];
            return;
        }

        NeuAnlegenAktiv = value.IstNeuAnlegen;
        if (value.IstNeuAnlegen)
            NeuFehler = null;
    }

    [RelayCommand]
    private async Task MannschaftAnlegenAsync()
    {
        NeuFehler = null;
        if (NeuLand is null)
        {
            NeuFehler = "Bitte ein Land auswählen.";
            return;
        }

        try
        {
            var eingabe = new MannschaftErstellenEingabe(
                NeuName, NeuKurzname,
                NeuIstNationalmannschaft ? Mannschaftsart.Nationalmannschaft : Mannschaftsart.Verein,
                NeuLand.Id,
                NeuOffensiv, NeuMittelfeld, NeuDefensiv,
                NeuHeimstaerke, NeuAuswaertsstaerke);

            var neu = await _mannschaftService.ErstellenAsync(eingabe);
            NeuAnlegenAktiv = false;
            await _mannschaftAngelegt(this, neu);
        }
        catch (Exception ex)
        {
            NeuFehler = ex.Message;
        }
    }

    [RelayCommand]
    private void NeuAnlegenAbbrechen()
    {
        NeuAnlegenAktiv = false;
        Ausgewaehlt = Optionen[0]; // "— Überspringen —"
    }
}
