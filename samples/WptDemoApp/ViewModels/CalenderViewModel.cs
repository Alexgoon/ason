using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ason;
using System.Collections.ObjectModel;
using System.Windows;
using WpfSampleApp.AI;
using WpfSampleApp.Model;
namespace WpfSampleApp.ViewModels;

public partial class CalendarViewModel(RootOperator rootOperator) : ObservableObject {
    [ObservableProperty]
    ObservableCollection<Appointment> appointments = new();

    bool isDataLoaded;

    [RelayCommand]
    async public Task LoadDataAsync() {
        if (isDataLoaded)
            return;
        Appointments = new ObservableCollection<Appointment>(await FakeDataService.GetAppointmentsAsync());
        isDataLoaded = true;
        rootOperator.AttachChildOperator<CalendarViewOperator>(this);
    }

    [RelayCommand]
    public void AddAppointmentAsync(Appointment appt) {
        Appointments.Add(appt);
    }
}
