using Ason;
using WpfSampleApp.Model;
using WpfSampleApp.ViewModels;
using WpfSampleApp.Views;

namespace WpfSampleApp.AI;

[ProxyClass(description: "Manages main navigation")]
public class MainAppOperator : RootOperator<MainViewModel> {
    public MainAppOperator(MainViewModel associatedObject) : base(associatedObject) { }

    [ProxyMethod("Navigates to Employees view")]
    public async Task<EmployeesViewOperator> GetEmployeesViewOperatorAsync() {
        return await GetViewOperator<EmployeesViewOperator>(Navigate<EmployeesViewModel>);
    }

    [ProxyMethod("Navigates to Calendar view")]
    public async Task<CalendarViewOperator> GetCalendarViewOperatorAsync() {
        return await GetViewOperator<CalendarViewOperator>(Navigate<CalendarViewModel>);
    }

    [ProxyMethod("Navigates to Emails view")]
    public async Task<EmailsViewOperator> GetEmailsViewOperatorAsync() {
        return await GetViewOperator<EmailsViewOperator>(Navigate<EmailsViewModel>);
    }

    [ProxyMethod("Naivgate to Charts view. CALL THIS METHOD WHEN USER TASK IS TO CREATE/DRAW CHART")]
    public async Task<ChartsViewOperator> GetChartsViewOperatorAsync() {
        return await GetViewOperator<ChartsViewOperator>(Navigate<ChartsViewModel>);
    }

    public void Navigate<TViewModelType>() {
        AttachedObject.CurrentNavigationItem = AttachedObject.NavigatoinItems.FirstOrDefault(i => i.ViewModel.GetType() == typeof(TViewModelType));
    }
}

[ProxyClass(description: "Manages Employees view")]
public class EmployeesViewOperator : OperatorBase<EmployeesViewModel> {

    [ProxyMethod]
    public void Test() => Console.WriteLine("CustomersViewOperator.Test called (host)");

    [ProxyMethod]
    public void ShowColumnChooser() => Console.WriteLine("CustomersViewOperator.ShowColumnChooser called (host)");

    [ProxyMethod]
    public List<Employee>? GetEmployees() {
        return AttachedObject?.Employees.ToList();
    }

    [ProxyMethod("Opens Employee editing tab to update employee data")]
    public async Task<EmployeeEditViewOperator> GetEmployeeEditingOperatorAsync(int employeeId) {
        return await GetViewOperator<EmployeeEditViewOperator>(() => AttachedObject?.EditEmployee(employeeId), employeeId.ToString());
    }
}

[ProxyClass("Manages employee update/editing operations")]
public class EmployeeEditViewOperator : OperatorBase<EmployeeEditViewModel> {

    [ProxyMethod]
    public void UpdateEmployee(Employee updatedEmployee) {
        if (AttachedObject != null)
            AttachedObject.Editable = updatedEmployee;
    }
}

[ProxyClass("Allows to create/draw charts")]
public class ChartsViewOperator : OperatorBase<ChartsView> {

    [ProxyMethod("Initializes bar chart and assigns data displayed in chart")]
    public void CreateBarChart(BarValue[] barValues, string xAxisCaption, string yAxisCaption) {
        AttachedObject?.AddBarChart(barValues, xAxisCaption, yAxisCaption);
    }
}

public class CalendarViewOperator : OperatorBase<CalendarViewModel> {

    [ProxyMethod]
    public void AddAppointment(Appointment appt) {
        AttachedObject?.AddAppointmentAsync(appt);
    }
}

public class EmailsViewOperator : OperatorBase<EmailsViewModel> {

    [ProxyMethod("Gets a list of emails")]
    public List<MailItem>? GetEmails() {
        return AttachedObject?.Emails.ToList();
    }
}