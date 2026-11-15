using CommunityToolkit.Mvvm.ComponentModel;
using Fussballturnierverwaltung.Application.Abstraktionen;
using Fussballturnierverwaltung.Application.Einstellungen;
using Fussballturnierverwaltung.Application.Validierung;
using Fussballturnierverwaltung.Infrastructure.Persistenz;
using System.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Felder der Datenbankverbindung (#49) – gemeinsam für die Karte auf der Seite „Datenbankwartung“ und das
/// Startfenster bei einem Verbindungsfehler. Prüft live, baut beim Übernehmen die Verbindung auf und speichert nur,
/// wenn sie steht. Erfolg wird nicht gemeldet, nur Fehler.</summary>
public partial class VerbindungsFormularViewModel : ObservableObject
{
    private readonly Verbindungseinstellung _einstellung;
    private readonly DatenbankVerbindungValidator _validator;
    private DatenbankVerbindung _gespeichert = new();
    private bool _laedt;

    [ObservableProperty] private string _server = "";
    [ObservableProperty] private string _port = "";
    [ObservableProperty] private string _datenbank = "";
    [ObservableProperty] private string _benutzer = "";
    [ObservableProperty] private string _kennwort = "";

    [ObservableProperty] private IReadOnlyList<Feldfehler> _fehler = [];
    [ObservableProperty] private string? _fehlermeldung;
    [ObservableProperty] private bool _laeuft;
    [ObservableProperty] private bool _istGeaendert;

    public VerbindungsFormularViewModel(Verbindungseinstellung einstellung, DatenbankVerbindungValidator validator)
    {
        _einstellung = einstellung;
        _validator = validator;
        Laden();
    }

    /// <summary>Die Verbindung kommt von außen (Umgebungsvariable der GUI-Tests) – Felder gesperrt.</summary>
    public bool IstGesperrt => _einstellung.IstVorgegeben;
    public bool IstBearbeitbar => !IstGesperrt;

    public string KennwortHinweis =>
        IstGesperrt ? "Von der Vorgabe bestimmt."
        : _gespeichert.KennwortVerschluesselt is null
            ? "Kein Kennwort gespeichert – PostgreSQL sucht es in seiner Kennwortdatei (pgpass.conf)."
            : "Gespeichert (verschlüsselt). Leer lassen = unverändert.";

    /// <summary>Darf übernommen werden: etwas geändert, gültig, nicht gesperrt und keine Prüfung im Gange.</summary>
    public bool KannUebernehmen => IstGeaendert && Fehler.Count == 0 && !IstGesperrt && !Laeuft;

    public void Laden()
    {
        _laedt = true;
        _gespeichert = _einstellung.Anzeigen();
        var eingabe = DatenbankVerbindungEingabe.Aus(_gespeichert);
        Server = eingabe.Server;
        Port = eingabe.Port;
        Datenbank = eingabe.Datenbank;
        Benutzer = eingabe.Benutzer;
        Kennwort = "";
        Fehler = [];
        Fehlermeldung = null;
        _laedt = false;
        IstGeaendert = false;
        OnPropertyChanged(nameof(KennwortHinweis));
    }

    /// <summary>Prüft die Eingabe, baut die Verbindung auf und speichert sie, wenn sie steht.</summary>
    /// <returns><c>true</c>, wenn gespeichert wurde.</returns>
    public async Task<bool> UebernehmenAsync()
    {
        var ergebnis = _validator.Validieren(Eingabe());
        Fehler = ergebnis.Meldungen;
        if (ergebnis.IstUngueltig || IstGesperrt) return false;

        Fehlermeldung = null;
        Laeuft = true;
        try
        {
            var verbindung = Eingabe().AlsVerbindung();
            var ursache = await Verbindungspruefung.PruefenAsync(_einstellung.ZeichenfolgeFuerEingabe(verbindung, Kennwort));
            if (ursache is not null)
            {
                Fehlermeldung = ursache;
                return false;
            }
            _einstellung.Speichern(verbindung, Kennwort);
            return true;
        }
        catch (Exception ex)
        {
            Fehlermeldung = $"Die Verbindung konnte nicht gespeichert werden: {ex.Message}";
            return false;
        }
        finally
        {
            Laeuft = false;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(Server) or nameof(Port) or nameof(Datenbank) or nameof(Benutzer) or nameof(Kennwort) when !_laedt:
                Fehler = _validator.Validieren(Eingabe()).Meldungen;
                Fehlermeldung = null;
                IstGeaendert = Eingabe().WeichtAbVon(_gespeichert) || Kennwort.Length > 0;
                break;
            case nameof(IstGeaendert) or nameof(Fehler) or nameof(Laeuft):
                OnPropertyChanged(nameof(KannUebernehmen));
                break;
        }
    }

    private DatenbankVerbindungEingabe Eingabe() => new(Server, Port, Datenbank, Benutzer);
}
