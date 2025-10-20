using Ason;
using Ason.CodeGen;
using BlazorAdvancedApp.State;
using BlazorAdvancedApp.Models;
using System.Linq;
using BlazorAdvancedApp.Components.Pages;
using Microsoft.AspNetCore.Components;
using BlazorAdvancedApp.Components; // for NavigationManager

namespace BlazorAdvancedApp.AI;

[AsonOperator]
public class BlazorMainAppOperator : RootOperator<SessionState> {

    public BlazorMainAppOperator(SessionState associated) : base(associated) { }

    [AsonMethod]
    public Task<EmployeesOperator> GetEmployeesOperatorAsync() {
        return GetViewOperator<EmployeesOperator>(() => AttachedObject.Nav.NavigateTo("/employees"));
    }

    [AsonMethod]
    public Task<EmailsOperator> GetEmailsOperatorAsync() {
        return GetViewOperator<EmailsOperator>(() => AttachedObject.Nav.NavigateTo("/emails"));
    }

    [AsonMethod("Gets charts operator. Call this when creating/drawing charts")]
    public Task<ChartsOperator> GetChartsOperatorAsync() {
        return GetViewOperator<ChartsOperator>(() => AttachedObject.Nav.NavigateTo("/charts"));
    }
}

[AsonOperator]
public class EmployeesOperator : OperatorBase<Employees> {
    [AsonMethod]
    public void Test() => Console.WriteLine("CustomersViewOperator.Test called (host)");

    [AsonMethod]
    public void ShowColumnChooser() => Console.WriteLine("CustomersViewOperator.ShowColumnChooser called (host)");

    [AsonMethod]
    public List<Employee> GetEmployees() => AttachedObject?.EmployeesSnapshot!;

    [AsonMethod]
    public void AddEmployee(Employee employee) => AttachedObject?.AddEmployee(employee);

    [AsonMethod]
    public void DeleteEmployee(int employeeId) => AttachedObject?.DeleteEmployee(employeeId);


    [AsonMethod("Opens Employee editing tab to update employee data")]
    public async Task<EmployeeEditViewOperator> GetEmployeeEditingOperatorAsync(int employeeId) {
        return await GetViewOperator<EmployeeEditViewOperator>(() => AttachedObject?.OpenEditor(employeeId), employeeId.ToString());
    }
}

[AsonOperator("Manages employee update/editing operations")]
public class EmployeeEditViewOperator : OperatorBase<EmployeeEditModel> {
    [AsonMethod]
    public void UpdateEmployee(Employee updatedEmployee) {
        AttachedObject!.ReplaceEditable(updatedEmployee);
    }
}

[AsonOperator(description: "Email operations: read latest and list emails")]
public class EmailsOperator : OperatorBase<SessionState> {

    [AsonMethod]
    public List<MailItem> GetEmails() => AttachedObject!.Emails.OrderByDescending(e => e.ReceivedDate).ToList();

    [AsonMethod("Get latest email body and subject")]
    public string GetLatestEmailSummary() { var mail = AttachedObject!.Emails.OrderByDescending(e => e.ReceivedDate).FirstOrDefault(); return mail is null ? "No emails" : $"Subject: {mail.Subject}\nBody: {mail.Body}"; }
}

[AsonOperator(description: "Chart operations: build simple sales charts")]
public class ChartsOperator : OperatorBase<Charts> {
    [AsonMethod("Initializes bar chart and assigns data displayed in chart")]
    public void CreateBarChart(BarValue[] barValues, string xAxisCaption, string yAxisCaption) {
        AttachedObject?.AddBarChart(barValues, xAxisCaption, yAxisCaption);
    }
}

[AsonModel("ALWAYS convert Value to double explicitly")]
public class BarValue {
    public string Caption { get; set; } = string.Empty;
    public double Value { get; set; }
}