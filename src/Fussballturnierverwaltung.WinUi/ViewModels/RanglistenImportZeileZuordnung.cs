using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Ranglisten.Import;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Application.Stammdaten.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;
using System.Collections.ObjectModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Auswahloption für die Zuordnungs-ComboBox einer nicht erkannten Zeile. Mannschaft null mit
/// <see cref="Art"/> Offen = „bitte zuordnen“, Ueberspringen = bewusst weglassen, NeuAnlegen blendet das
/// Inline-Schnell-Anlegen-Formular der Zeile ein (Issue #227; inline statt Dialog, da WinUI 3 keinen zweiten
/// ContentDialog öffnen kann, und so auch im Reiter beibehalten, #477).</summary>
public record MannschaftAuswahlOption(string Anzeige, Mannschaft? Mannschaft, AuswahlArt Art = AuswahlArt.Mannschaft);

public enum AuswahlArt { Mannschaft, Offen, Ueberspringen, NeuAnlegen }

/// <summary>Eine Zeile der Import-Tabelle (#477) in Quellreihenfolge — automatisch erkannt (fest) oder nicht erkannt
/// (Zuordnung per ComboBox). Trägt Service/Länder und den Anlegen-Befehl selbst: Ein ItemTemplate einer ListView hat
/// in WinUI 3 keinen zuverlässigen Weg zurück zum DataContext des umschließenden Elements (kein FindAncestor). Der
/// Callback bietet eine neu angelegte Mannschaft allen Geschwister-Zeilen an (Issue #227).</summary>
public partial class RanglistenImportZeileZuordnung : ObservableObject
{
    public static readonly MannschaftAuswahlOption OffenOption = new("— bitte zuordnen —", null, AuswahlArt.Offen);
    public static readonly MannschaftAuswahlOption UeberspringenOption = new("— Überspringen —", null, AuswahlArt.Ueberspringen);
    public static readonly MannschaftAuswahlOption NeuAnlegenOption = new("+ Neue Mannschaft anlegen…", null, AuswahlArt.NeuAnlegen);

    private readonly MannschaftService? _mannschaftService;
    private readonly Func<RanglistenImportZeileZuordnung, Mannschaft, Task>? _mannschaftAngelegt;

    public RanglistenRohZeile Quelle { get; }
    public int Nummer => Quelle.Index + 1;
    public string PunkteText => Quelle.Punkte?.ToString() ?? "";

    /// <summary>Automatisch erkannte Mannschaft; null bei einer nicht erkannten Zeile.</summary>
    public Mannschaft? Erkannt { get; }
    public bool IstAutomatisch => Erkannt is not null;
    public bool IstNichtErkannt => Erkannt is null;

    public ObservableCollection<MannschaftAuswahlOption> Optionen { get; }
    public IReadOnlyList<Land> Laender { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Auswahl))]
    [NotifyPropertyChangedFor(nameof(Status))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusDeckkraft))]
    private MannschaftAuswahlOption _ausgewaehlt = OffenOption;

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

    /// <summary>Meldet jede Statusänderung, damit der Reiter seine Zusammenfassung nachzieht.</summary>
    public event EventHandler? StatusGeaendert;

    private RanglistenImportZeileZuordnung(
        RanglistenRohZeile quelle,
        Mannschaft? erkannt,
        IReadOnlyList<MannschaftAuswahlOption> optionen,
        IReadOnlyList<Land> laender,
        MannschaftService? mannschaftService,
        Func<RanglistenImportZeileZuordnung, Mannschaft, Task>? mannschaftAngelegt)
    {
        Quelle = quelle;
        Erkannt = erkannt;
        Optionen = new ObservableCollection<MannschaftAuswahlOption>(optionen);
        Laender = laender;
        _mannschaftService = mannschaftService;
        _mannschaftAngelegt = mannschaftAngelegt;
        _neuName = quelle.RohName;
        _neuKurzname = new string(quelle.RohName.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
    }

    public static RanglistenImportZeileZuordnung Automatisch(RanglistenImportZeile zeile) =>
        new(zeile.Quelle, zeile.Mannschaft, [], [], null, null);

    public static RanglistenImportZeileZuordnung NichtErkannt(
        RanglistenRohZeile quelle,
        IReadOnlyList<MannschaftAuswahlOption> optionen,
        IReadOnlyList<Land> laender,
        MannschaftService mannschaftService,
        Func<RanglistenImportZeileZuordnung, Mannschaft, Task> mannschaftAngelegt) =>
        new(quelle, null, optionen, laender, mannschaftService, mannschaftAngelegt);

    /// <summary>Mannschaft, die importiert wird; null = Zeile fällt weg (offen oder übersprungen).</summary>
    public Mannschaft? Mannschaft => Erkannt ?? Ausgewaehlt.Mannschaft;

    public ImportZeilenStatus Status => IstAutomatisch
        ? ImportZeilenStatus.Automatisch
        : Ausgewaehlt.Art switch
        {
            AuswahlArt.Mannschaft => ImportZeilenStatus.Zugeordnet,
            AuswahlArt.Ueberspringen => ImportZeilenStatus.Uebersprungen,
            _ => ImportZeilenStatus.Offen
        };

    public string StatusText => Status switch
    {
        ImportZeilenStatus.Automatisch => "✓ automatisch",
        ImportZeilenStatus.Zugeordnet => "✓ zugeordnet",
        ImportZeilenStatus.Uebersprungen => "– übersprungen",
        _ => "? offen"
    };

    // Ohne Signalfarben (Grün/Rot sind für Erfolg/Fehler reserviert): offene Zeilen voll, erledigte abgeblendet.
    public double StatusDeckkraft => Status == ImportZeilenStatus.Offen ? 1.0 : 0.6;

    // Bindungshülle der ComboBox: WinUI schreibt beim Tausch der ItemsSource bzw. beim Recycling der ListView-Zeilen
    // null über TwoWay zurück (#377). null wird verworfen und die Anzeige danach wiederhergestellt — sonst ginge bei
    // langen Tabellen beim Scrollen eine Zuordnung verloren.
    public MannschaftAuswahlOption? Auswahl
    {
        get => IstNichtErkannt ? Ausgewaehlt : null;
        set
        {
            if (value is not null) Ausgewaehlt = value;
            else Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => OnPropertyChanged(nameof(Auswahl)));
        }
    }

    partial void OnAusgewaehltChanged(MannschaftAuswahlOption value)
    {
        NeuAnlegenAktiv = value.Art == AuswahlArt.NeuAnlegen;
        if (NeuAnlegenAktiv)
            NeuFehler = null;
        StatusGeaendert?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task MannschaftAnlegenAsync()
    {
        if (_mannschaftService is null || _mannschaftAngelegt is null) return;

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
        Ausgewaehlt = OffenOption;
    }
}
