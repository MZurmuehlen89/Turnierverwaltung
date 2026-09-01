using CommunityToolkit.Mvvm.ComponentModel;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public virtual Task AktivierenAsync() => Task.CompletedTask;
}
