# CLAUDE.md — WinUi

Gilt zusätzlich zur Root-`CLAUDE.md` bei Arbeit an diesem Projekt.

## WinUi Patterns

**`ViewModelTemplateSelector`** (`src/Fussballturnierverwaltung.WinUi/ViewModelTemplateSelector.cs`) is the
WinUI3 equivalent of the WPF `MainWindow.xaml` implicit `DataType`-`DataTemplate`s: `x:Bind` is strongly typed
and cannot map `MainViewModel.AktuellesViewModel` (`ViewModelBase`) to the concrete View at compile time, and
WinUI 3 does not reliably resolve implicit `DataTemplate`s for a bare `ContentControl` the way WPF does. Adding
a new Fach-View therefore means: add a `case` to `SelectTemplateCore` + a `DataTemplate` property on the
selector, declare the `DataTemplate` as a resource in `MainWindow.xaml`, and wire the property via
`{StaticResource ...}` on `ViewModelTemplateSelector` in `MainWindow.xaml` — omitting this compiles fine and
navigates without error, but the content area silently stays blank.

**`IDialogService`** replaces WPF's `MessageBox.Show` (not available in WinUI 3). It holds the `XamlRoot` set
by `MainWindow` at startup and shows a `ContentDialog` — `NavigationService.DarfWegnavigierenAsync` depends on
it instead of calling a static dialog API directly.

**`[ObservableProperty]` stays on `_camelCase` private fields, not partial properties**, even though
CommunityToolkit.Mvvm 8.4 recommends partial properties for WinRT/AOT scenarios (warning `MVVMTK0045`): in this
project's WinUI3 setup, a `partial` property with `[ObservableProperty]` fails to compile (`CS9248`, generator
does not produce the implementation part) — root cause not identified, and not worth chasing since
`PublishTrimmed`/AOT is not used here. The `MVVMTK0045` warning is expected and switched off in `.globalconfig` (#498).

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
dialog (found while porting the Ranglisten-Import in #285; the import was removed in #596). Two independent
workarounds: (1) merge a parent-dialog + child-dialog pair into one ContentDialog with a list+form layout, or
better make it a page tab, which avoids the problem entirely; (2) replace a child dialog with an inline expanding
form directly in the triggering row/element instead of a popup. A `ViewModel`-owned
`ContentDialog` also has no `XamlRoot` of its own — `IDialogService.AnzeigenAsync(ContentDialog)` sets it
centrally (mirrors the WPF original, where the ViewModel directly opens the `Window`; see
`TurnierkopfViewModel.AusVorlageErstellenAsync`).

**`Flyout.Content` sizing is fought over by two containers**: the System-provided `FlyoutPresenter` that wraps
whatever you assign to `Flyout.Content` has its own default `MaxWidth` and its own internal scroll handling,
independent of any `ScrollViewer` you put inside `Content` yourself. A `ScrollViewer` with
`HorizontalScrollBarVisibility="Disabled"` inside `Content` does not prevent a horizontal scrollbar if the
outer `FlyoutPresenter` itself is narrower than your content — that scrollbar comes from the `FlyoutPresenter`,
not your own `ScrollViewer`, and widening your own content or clamping a child's `MaxWidth` has no effect on
it. Fix: set `Flyout.FlyoutPresenterStyle` with a `Setter` raising `FrameworkElement.MaxWidthProperty` to at
least your content's width (see `MainWindow.ToggleFlyoutErstellen`, #310).

**A `ComboBox` that receives its `SelectedItem` before it is `Loaded` drops it again on first load and writes
`null` back through the TwoWay binding** — happens whenever a page is recreated (e.g. switching in from another
editor) and a form is populated before its ComboBoxes finish loading (log: `SelectionChanged Removed=[x]`,
`Index=-1`, only then `Loaded`). For enum-typed properties the `null` arrives as the enum's default value (e.g.
`Rundenart.Liga`) and is indistinguishable from a real choice — the record silently changes. Bind enum ComboBoxes
to a nullable wrapper property that ignores `null` and re-raises `PropertyChanged` on a low-priority
`DispatcherQueue` tick; option-record ComboBoxes likewise restore their value from the backing record instead of
keeping `null` (see `RundenFormularViewModel.RundenartAuswahl`).

**`CheckBox`/`ToggleButton`-derived controls reserve significant content space even with no `Content` set** —
their `ButtonBase` ancestry defaults to a `MinWidth` around 120px that applies to the whole control regardless
of column/grid sizing around it. Prefer setting the label/description as the control's own `Content` (a
`StackPanel` with `TextBlock`s works and stays clickable as part of the toggle) over placing it in a sibling
cell next to an empty-`Content` `CheckBox` — the latter reserves the ~120px gap no matter how the grid column
is computed (#310).

**A grouped `ListView` needs its `CollectionViewSource` wired in code-behind** — resources do not inherit the DataContext, so `Source="{Binding …}"` on a `CollectionViewSource` in `UserControl.Resources` stays empty. Set `Source` and the list's `ItemsSource = cvs.View` on `DataContextChanged`; the groups are `ObservableCollection`s with a header property that raises `PropertyChanged` (see `TeilnehmerpoolAnsicht`, `TeilnehmerpoolGruppe`, #576).

## AutomationIds für GUI-Tests (#494, #521)

Elements a GUI test needs get an **explicit** `AutomationProperties.AutomationId` in the schema `View.Element`
(German, PascalCase, no umlauts: `Auslosung.Durchfuehren`, `Navigation.Turnierdurchfuehrung`). Never rely on the
AutomationId WinUI derives from `x:Name` — `x:Name` cannot contain a dot and changes with refactorings. IDs grow
with the test scenarios; there is no big-bang pass over all views.

- In `DataTemplate`s bind the ID (`AutomationProperties.AutomationId="{x:Bind AutomationId}"`, see
  `NavigationsEintrag`).
- Only elements with an automation peer appear in the UIA tree: controls and `TextBlock` yes, `Grid`,
  `StackPanel`, `UserControl` roots no — an ID on those is invisible to tests. A `UserControl` that is used several
  times on one page and must be read as a unit gets its own peer
  (`OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this)`, see `SpielKopfControl`, #536); the ID then
  goes on the usage site and the inner IDs are searched inside it.
- `Hauptfenster.Befehlsleiste` carries the current page title as its UIA name (`MainViewModel.RibbonTabTitel` — the
  visible title is collapsed on pages without ribbon area and then missing from the tree) and the load state as
  `AutomationProperties.ItemStatus` (`Bereit` / `Beschaeftigt`, `LadeanzeigeViewModel.Ladezustand`) without the
  300 ms delay of the visible bar; tests wait on both. The state only sees open `AblaufVorgang`s — data loaded
  without one counts as `Bereit`. `NavigationService` therefore wraps every `AktivierenAsync` in a vorgang
  („Seite öffnen: …“); without it, tests on the slower CI runner acted on half-loaded pages (#525). Data loaded
  later (after a click, a selection) needs its own vorgang for the same reason.
- Collapsed elements (`Visibility="Collapsed"`) are not in the UIA tree at all — never hang a test signal on them.
- Form messages live in the shared `Views/MeldungsLeiste` (#594: edge with counter on the right, list folds out over
  the form). Folded, the messages are collapsed — the GUI tests read them through the page object `MeldungsLeiste`,
  which unfolds the edge `<Präfix>.Meldungen` first. A save error unfolds the list by itself, live-check errors don't.
- `ListView` items report their data object's `ToString()` as UIA name, `ItemsControl` rows are flat text — both
  need explicit names/containers once a test addresses them (findings in #520).
