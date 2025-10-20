using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ason;
using System.Collections.ObjectModel;
using WpfSampleApp.AI;
using WpfSampleApp.Model;

namespace WpfSampleApp.ViewModels;

public partial class EmailsViewModel(RootOperator rootOperator) : ObservableObject {
    [ObservableProperty]
    ObservableCollection<MailItem> emails = new();
    [ObservableProperty]
    MailItem? selectedEmail;

    bool isDataLoaded;

    [RelayCommand]
    async public Task LoadDataAsync() {
        if (isDataLoaded)
            return;
        Emails = new ObservableCollection<MailItem>(await FakeDataService.GetMailItemsAsync());
        SelectedEmail = Emails.FirstOrDefault();
        rootOperator.AttachChildOperator<EmailsViewOperator>(this);
        isDataLoaded = true;
    }
}
