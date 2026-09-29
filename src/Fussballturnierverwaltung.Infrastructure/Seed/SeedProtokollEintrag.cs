namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>Ein Schritt des Seeds/Nachrüstens, der tatsächlich etwas angelegt oder ergänzt hat (Issue #370),
/// z. B. <c>12</c> + "Mannschaften nachgerüstet.".</summary>
public record SeedProtokollEintrag(int Anzahl, string Text);
