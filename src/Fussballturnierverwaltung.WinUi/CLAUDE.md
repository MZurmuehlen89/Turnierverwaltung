# CLAUDE.md — WinUi

Gilt zusätzlich zur Root-`CLAUDE.md` bei Arbeit an diesem Projekt.

## WinUi Patterns

The WinUI 3 project (`Fussballturnierverwaltung.WinUi`) is the target of the ongoing WPF-to-WinUI3 migration
(#279). Its own `Navigation`/`Ribbon` types are deliberate independent copies of the WPF originals (both
startup projects must stay independent) — same names, same shape, but no shared assembly.

**`ViewModelTemplateSelector`** (`src/Fussballturnierverwaltung.WinUi/ViewModelTemplateSelector.cs`) is the
WinUI3 equivalent of the WPF `MainWindow.xaml` implicit `DataType`-`DataTemplate`s: `x:Bind` is strongly typed
and cannot map `MainViewModel.AktuellesViewModel` (`ViewModelBase`) to the concrete View at compile time, and
WinUI 3 does not reliably resolve implicit `DataTemplate`s for a bare `ContentControl` the way WPF does. Adding
a new Fach-View therefore means: add a `case` to `SelectTemplateCore` + a `DataTemplate` property on the
selector, declare the `DataTemplate` as a resource in `MainWindow.xaml`, and wire the property via
`{StaticResource ...}` on `ViewModelTemplateSelector` in `MainWindow.xaml` — omitting this compiles fine and
navigates without error, but the content area silently stays blank (analogous silent-failure risk to the WPF
`DataTemplate`-omission pitfall in "Adding a new feature" in the root `CLAUDE.md`).

**`IDialogService`** replaces WPF's `MessageBox.Show` (not available in WinUI 3). It holds the `XamlRoot` set
by `MainWindow` at startup and shows a `ContentDialog` — `NavigationService.DarfWegnavigierenAsync` depends on
it instead of calling a static dialog API directly.

**`[ObservableProperty]` stays on `_camelCase` private fields, not partial properties**, even though
CommunityToolkit.Mvvm 8.4 recommends partial properties for WinRT/AOT scenarios (warning `MVVMTK0045`): in this
project's WinUI3 setup, a `partial` property with `[ObservableProperty]` fails to compile (`CS9248`, generator
does not produce the implementation part) — root cause not identified, and not worth chasing since
`PublishTrimmed`/AOT is not used here. The `MVVMTK0045` warning is expected and intentionally left unresolved.

**CommandBar has no `ItemsSource` binding** for `PrimaryCommands`/`SecondaryCommands` (unlike WPF's
`fluent:RibbonGroupBox`, which binds children declaratively). `IRibbonAware.RibbonZusatzGruppen` is therefore
synced into `CommandBar.PrimaryCommands` imperatively in `MainWindow.xaml.cs`
(`RibbonZusatzGruppenAktualisieren`, triggered by `MainViewModel.PropertyChanged`) rather than via XAML binding.

**The whole `CommandBar` must stay visible regardless of `HatRibbonBereich`** — only bind that flag to
individual elements inside it (the Fach-View title, the standard-action buttons). App-wide actions like the
Dark-Mode toggle live in the same `CommandBar` and must stay reachable even when the active View is not
`IRibbonAware` (e.g. `PlatzhalterViewModel`); binding `Visibility` on the `CommandBar` itself hides everything
inside it, including those app-wide actions (found in #280's manual test).

**`MainWindow`'s root `Grid` needs an explicit `Background="{ThemeResource ApplicationPageBackgroundThemeBrush}"`.**
Without it the grid stays transparent and WinUI 3's window backdrop (Mica) follows the *system* theme instead
of the app-internal `RequestedTheme` — if they diverge (e.g. system in dark, app in light), text and background
mismatch (found in #280's manual test).

**`ThemeService.AktuellerModus` must be loaded in its constructor, not lazily on first apply.** `MainViewModel`
is resolved via DI before `MainWindow` exists (its constructor is called first, and `MainWindow` is the one
that calls `ThemeService.AnwendenAusEinstellungen()`) — if `AktuellerModus` only gets its real value on that
later call, `MainViewModel.IstDunkelModus` (read in *its* constructor) captures the stale default and the
Dark-Mode toggle shows the wrong state until the user clicks it once (found in #280's manual test).

**Only one `ContentDialog` can be shown at a time per `XamlRoot`** — a second `ShowAsync()` while one is
already open throws. This blocks any WPF-Window-dialog pattern that nests a child dialog inside a parent
dialog (found while porting Ranglisten-Import in #285, which in WPF opens a "Quellen verwalten" child window
from inside the import window, and a "Mannschaft schnell anlegen" child window from inside an unresolved
import row). Two independent workarounds used there: (1) merge a parent-dialog + child-dialog pair into one
ContentDialog with an app-wide list+form layout instead of two windows (`RanglistenQuellenVerwaltenDialog`);
(2) replace a child dialog with an inline expanding form directly in the triggering row/element instead of a
popup (`UnaufgeloesteZeileZuordnung.NeuAnlegenAktiv` in `RanglistenImportDialog`). A `ViewModel`-owned
`ContentDialog` also has no `XamlRoot` of its own — `IDialogService.AnzeigenAsync(ContentDialog)` sets it
centrally (mirrors the WPF original, where the ViewModel directly opens the `Window`; see
`RanglistenViewModel.ImportAusInternet`/`QuellenVerwalten`).
