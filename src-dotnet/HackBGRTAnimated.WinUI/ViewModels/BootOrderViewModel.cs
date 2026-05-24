using System.Collections.ObjectModel;

namespace HackBGRTAnimated.WinUI.ViewModels;

public sealed class BootOrderViewModel : ObservableObject
{
    public ObservableCollection<BootOrderItem> Entries { get; } = [];
}
