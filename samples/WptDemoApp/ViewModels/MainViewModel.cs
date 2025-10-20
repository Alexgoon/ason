using CommunityToolkit.Mvvm.ComponentModel;
using Ason;
using System.Collections.ObjectModel;
using WpfSampleApp.AI;
using WpfSampleApp.Model;

namespace WpfSampleApp.ViewModels;

public partial class MainViewModel : ObservableObject {
    [ObservableProperty]
    object? currentNavigationItem;

    [ObservableProperty]
    ChatViewModel? chatViewModel;

    [ObservableProperty]
    ObservableCollection<NavigatoinItem> navigatoinItems = new();

    public RootOperator MainAppOperator;

    public MainViewModel() {
        MainAppOperator = new MainAppOperator(this);
        navigatoinItems = new ObservableCollection<NavigatoinItem>() {
            new NavigatoinItem("Employees",  () => new EmployeesViewModel(MainAppOperator)),
            new NavigatoinItem("Calendar", () => new CalendarViewModel(MainAppOperator)),
            new NavigatoinItem("Emails", () => new EmailsViewModel(MainAppOperator)),
            new NavigatoinItem("Analytics", () => new ChartsViewModel(MainAppOperator))
        };
        CurrentNavigationItem = navigatoinItems.FirstOrDefault();
        ChatViewModel = new ChatViewModel(this);
    }


}
