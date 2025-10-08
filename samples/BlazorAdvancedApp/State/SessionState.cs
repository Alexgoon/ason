using BlazorAdvancedApp.Models;
using System.Collections.ObjectModel;
using Ason;
using Ason.CodeGen;
using BlazorAdvancedApp.AI; // added
using Microsoft.AspNetCore.Components; // for NavigationManager
using BlazorAdvancedApp.Services; // added for data service

namespace BlazorAdvancedApp.State;


public class SessionState {

    public ObservableCollection<Employee> Employees { get; } = new();
    public ObservableCollection<Appointment> Appointments { get; } = new();
    public ObservableCollection<MailItem> Emails { get; } = new();

    // Chart state
    public List<string> ChartLabels { get; } = new();
    public List<double> ChartValues { get; } = new();
    public string? ChartDescription { get; set; }

    bool _seeded;

    public RootOperator MainAppOperator { get; }

    public NavigationManager Nav { get; }

    readonly IAppDataService _dataService;

    // DI supplies NavigationManager + data service
    public SessionState(NavigationManager nav, IAppDataService dataService) {
        Nav = nav;
        _dataService = dataService;
        MainAppOperator = new BlazorMainAppOperator(this); // defined in AI/operators file
    }

    public async Task EnsureSeededAsync() {
        if (_seeded) return;
        _seeded = true;
        var employees = await _dataService.GetEmployeesAsync();
        foreach (var e in employees) Employees.Add(e);
        var mails = await _dataService.GetMailItemsAsync();
        foreach (var m in mails) Emails.Add(m);
        var appts = await _dataService.GetAppointmentsAsync();
        foreach (var a in appts) Appointments.Add(a);
    }
}
