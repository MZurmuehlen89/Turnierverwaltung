namespace Fussballturnierverwaltung.Application.Ranglisten.Import;

public interface IHtmlHerunterlader
{
    Task<string> HerunterladenAsync(string url, CancellationToken ct = default);
}
