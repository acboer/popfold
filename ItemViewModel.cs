using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Popfold;

public sealed class ItemViewModel : INotifyPropertyChanged
{
    private ImageSource _icon;

    public ItemViewModel(FolderItem model)
    {
        Model = model;
        _icon = IconProvider.GetIcon(model, out bool pending);
        if (pending) _ = LoadUrlIconAsync();
    }

    public FolderItem Model { get; }
    public string Name => Model.Name;
    public string ToolTipText => Model.Path;
    public Visibility FileVisibility => Model.Kind == ItemKind.File ? Visibility.Visible : Visibility.Collapsed;

    public ImageSource Icon
    {
        get => _icon;
        private set { _icon = value; OnPropertyChanged(); }
    }

    private async Task LoadUrlIconAsync()
    {
        var img = await IconProvider.LoadUrlIconAsync(Model);
        if (img != null) Icon = img;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
