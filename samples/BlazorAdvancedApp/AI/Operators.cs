using Ason;
using Ason.CodeGen;
using BlazorAdvancedApp.State;
using BlazorAdvancedApp.Models;
using System.Linq;
using BlazorAdvancedApp.Components.Pages;
using Microsoft.AspNetCore.Components;
using BlazorAdvancedApp.Components; // for NavigationManager

namespace BlazorAdvancedApp.AI;

[AsonClass]
public class BlazorMainAppOperator : RootOperator<SessionState> {

    public BlazorMainAppOperator(SessionState associated) : base(associated) { }

    [ProxyMethod]
    public Task<EmployeesOperator> GetEmployeesOperatorAsync() {
        return GetViewOperator<EmployeesOperator>(() => { AttachedObject.Nav.NavigateTo("/employees"); });
    }

    [ProxyMethod]
    public Task<EmailsOperator> GetEmailsOperatorAsync() {
        return GetViewOperator<EmailsOperator>(() => { AttachedObject.Nav.NavigateTo("/emails"); });
    }

    [ProxyMethod("Gets charts operator. Call this when creating/drawing charts")]
    public Task<ChartsOperator> GetChartsOperatorAsync() {
        return GetViewOperator<ChartsOperator>(() => { AttachedObject.Nav.NavigateTo("/charts"); });
    }
}

[AsonClass]
public class EmployeesOperator : OperatorBase<Employees> {
    [ProxyMethod]
    public void Test() => Console.WriteLine("CustomersViewOperator.Test called (host)");

    [ProxyMethod]
    public void ShowColumnChooser() => Console.WriteLine("CustomersViewOperator.ShowColumnChooser called (host)");

    [ProxyMethod]
    public List<Employee> GetEmployees() => AttachedObject?.EmployeesSnapshot!;

    [ProxyMethod]
    public void AddEmployee(Employee employee) => AttachedObject?.AddEmployee(employee);

    [ProxyMethod]
    public void DeleteEmployee(int employeeId) => AttachedObject?.DeleteEmployee(employeeId);


    [ProxyMethod("Opens Employee editing tab to update employee data")]
    public async Task<EmployeeEditViewOperator> GetEmployeeEditingOperatorAsync(int employeeId) {
        return await GetViewOperator<EmployeeEditViewOperator>(() => AttachedObject?.OpenEditor(employeeId), employeeId.ToString());
    }
}

[AsonClass("Manages employee update/editing operations")]
public class EmployeeEditViewOperator : OperatorBase<EmployeeEditModel> {
    [ProxyMethod]
    public void UpdateEmployee(Employee updatedEmployee) {
        AttachedObject!.ReplaceEditable(updatedEmployee);
    }
}

[AsonClass(description: "Email operations: read latest and list emails")]
public class EmailsOperator : OperatorBase<SessionState> {

    [ProxyMethod]
    public List<MailItem> GetEmails() => AttachedObject!.Emails.OrderByDescending(e => e.ReceivedDate).ToList();

    [ProxyMethod("Get latest email body and subject")]
    public string GetLatestEmailSummary() { var mail = AttachedObject!.Emails.OrderByDescending(e => e.ReceivedDate).FirstOrDefault(); return mail is null ? "No emails" : $"Subject: {mail.Subject}\nBody: {mail.Body}"; }
}

[AsonClass(description: "Chart operations: build simple sales charts")]
public class ChartsOperator : OperatorBase<Charts> {
    [ProxyMethod("Initializes bar chart and assigns data displayed in chart")]
    public void CreateBarChart(BarValue[] barValues, string xAxisCaption, string yAxisCaption) {
        AttachedObject?.AddBarChart(barValues, xAxisCaption, yAxisCaption);
    }
}

[AsonModel("ALWAYS convert Value to double explicitly")]
public class BarValue {
    public string Caption { get; set; } = string.Empty;
    public double Value { get; set; }
}