using Fussballturnierverwaltung.Application.Ranglisten.Import;

namespace Fussballturnierverwaltung.Tests.Ranglisten;

/// <summary>Stub für IHtmlHerunterlader — liefert festes HTML statt echter HTTP-Anfrage.</summary>
public class FakeHtmlHerunterlader(string html) : IHtmlHerunterlader
{
    public Task<string> HerunterladenAsync(string url, CancellationToken ct = default)
        => Task.FromResult(html);
}
