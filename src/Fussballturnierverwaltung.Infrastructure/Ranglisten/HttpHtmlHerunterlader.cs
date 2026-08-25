using Fussballturnierverwaltung.Application.Ranglisten.Import;

namespace Fussballturnierverwaltung.Infrastructure.Ranglisten;

public class HttpHtmlHerunterlader(HttpClient httpClient) : IHtmlHerunterlader
{
    public async Task<string> HerunterladenAsync(string url, CancellationToken ct = default)
    {
        using var antwort = await httpClient.GetAsync(url, ct);
        antwort.EnsureSuccessStatusCode();
        return await antwort.Content.ReadAsStringAsync(ct);
    }
}
