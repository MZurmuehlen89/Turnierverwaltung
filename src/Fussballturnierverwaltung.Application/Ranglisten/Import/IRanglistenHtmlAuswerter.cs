using Fussballturnierverwaltung.Domain.Entitaeten.Ranglisten;

namespace Fussballturnierverwaltung.Application.Ranglisten.Import;

public interface IRanglistenHtmlAuswerter
{
    IReadOnlyList<RanglistenRohZeile> Auswerten(string html, RanglistenQuelle quelle);
}
