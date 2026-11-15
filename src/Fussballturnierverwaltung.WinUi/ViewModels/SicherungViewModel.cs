using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fussballturnierverwaltung.Application.Einstellungen;
using Fussballturnierverwaltung.Infrastructure.Wartung;
using Fussballturnierverwaltung.WinUi.Dialoge;
using Fussballturnierverwaltung.WinUi.Helpers;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Eine Zeile der Sicherungsliste; der Name ist zugleich der UIA-Name der Zeile.</summary>
public sealed record SicherungsZeile(Sicherung Sicherung)
{
    private static readonly CultureInfo _deutsch = CultureInfo.GetCultureInfo("de-DE");

    public string Name => Sicherung.Name;
    public string Zeitpunkt => Sicherung.Zeitpunkt.ToString("dd.MM.yyyy HH:mm", _deutsch);
    public string Groesse => Sicherung.Groesse >= 1024 * 1024
        ? (Sicherung.Groesse / 1024d / 1024d).ToString("N1", _deutsch) + " MB"
        : Math.Max(1, Sicherung.Groesse / 1024).ToString("N0", _deutsch) + " KB";

    public override string ToString() => Name;
}

/// <summary>Issue #43: Karte „Sicherungen“ der Datenbankwartung – sichern und wiederherstellen auf Knopfdruck, keine
/// automatische Sicherung. Die neue Sicherung erscheint in der Liste statt einer Erfolgsmeldung; nach dem
/// Wiederherstellen startet die App neu, weil ihre Seiten noch den alten Stand zeigen.</summary>
public partial class SicherungViewModel : ObservableObject
{
    private readonly DatenbankSicherungService _dienst;
    private readonly IDialogService _dialog;

    /// <summary>Was gerade läuft („Sichere…“), <c>null</c> in Ruhe – sperrt solange alle Befehle der Karte.</summary>
    [ObservableProperty] private string? _taetigkeit;
    [ObservableProperty] private string? _fehlermeldung;
    [ObservableProperty] private SicherungsZeile? _ausgewaehlteSicherung;

    public ObservableCollection<SicherungsZeile> Sicherungen { get; } = [];
    public bool KeineSicherung => Sicherungen.Count == 0;
    public string Ordner => Datenpfade.Sicherungen;

    public AsyncRelayCommand SichernBefehl { get; }
    public AsyncRelayCommand WiederherstellenBefehl { get; }
    public RelayCommand OrdnerOeffnenBefehl { get; }

    public SicherungViewModel(DatenbankSicherungService dienst, IDialogService dialog)
    {
        _dienst = dienst;
        _dialog = dialog;
        SichernBefehl = new AsyncRelayCommand(SichernAsync, () => !Laeuft);
        WiederherstellenBefehl = new AsyncRelayCommand(WiederherstellenAsync,
            () => !Laeuft && AusgewaehlteSicherung is not null);
        OrdnerOeffnenBefehl = new RelayCommand(OrdnerOeffnen);
    }

    /// <summary>Wahr, solange Sichern oder Wiederherstellen läuft – die Seite sperrt dann ihre übrigen Befehle, die
    /// sonst eigene Verbindungen auf die Datenbank öffneten, die gerade getauscht wird.</summary>
    public bool Laeuft => Taetigkeit is not null;

    public void Laden()
    {
        Sicherungen.Clear();
        try
        {
            foreach (var sicherung in DatenbankSicherungService.Auflisten(Ordner))
                Sicherungen.Add(new SicherungsZeile(sicherung));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fehlermeldung = $"Der Sicherungsordner ließ sich nicht lesen: {ex.Message}";
        }
        OnPropertyChanged(nameof(KeineSicherung));
    }

    partial void OnTaetigkeitChanged(string? value)
    {
        OnPropertyChanged(nameof(Laeuft));
        BefehleAktualisieren();
    }
    partial void OnAusgewaehlteSicherungChanged(SicherungsZeile? value) => BefehleAktualisieren();

    private void BefehleAktualisieren()
    {
        SichernBefehl.NotifyCanExecuteChanged();
        WiederherstellenBefehl.NotifyCanExecuteChanged();
    }

    private Task SichernAsync() => AusfuehrenAsync("Sichere die Datenbank…", () => _dienst.SichernAsync(Ordner));

    private async Task WiederherstellenAsync()
    {
        if (AusgewaehlteSicherung is not { } zeile)
            return;
        if (!await _dialog.BestaetigenAsync("Datenbank wiederherstellen?",
                $"Der aktuelle Stand der Datenbank wird vollständig durch die Sicherung „{zeile.Name}“ vom "
                + $"{zeile.Zeitpunkt} ersetzt. Alle Änderungen seit dieser Sicherung gehen verloren.\n\n"
                + "Danach startet das Programm neu."))
            return;

        await AusfuehrenAsync("Stelle die Datenbank wieder her…", async () =>
        {
            await _dienst.WiederherstellenAsync(zeile.Sicherung.Pfad);
            Fehlermeldung = AppNeustart.Ausfuehren("die Datenbank ist wiederhergestellt.");
        });
    }

    private async Task AusfuehrenAsync(string taetigkeit, Func<Task> aktion)
    {
        Fehlermeldung = null;
        Taetigkeit = taetigkeit;
        try
        {
            await aktion();
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            Taetigkeit = null;
            Laden();
        }
    }

    private void OrdnerOeffnen()
    {
        Directory.CreateDirectory(Ordner);
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        Process.Start(new ProcessStartInfo(explorer) { ArgumentList = { Ordner } });
    }
}
