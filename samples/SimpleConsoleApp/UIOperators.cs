using Microsoft.CodeAnalysis.CSharp.Syntax;
using Ason;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyConsoleApp;
// UI Operators moved to the host app
//[ProxyClass(description: "Manages the main navigation", targetName: null)]
//public static class MainAppOperator {
//    public static ViewModels.AccordionShellViewModel? Navigator; // may be assigned by host

//    static readonly Dictionary<Type, object> operatorTaskCompletions = new();
//    static readonly Dictionary<Type, object> existingOperators = new();

//    public static void CompleteInitialization<TViewOperator>(TViewOperator viewOperator) {
//        if (operatorTaskCompletions.TryGetValue(typeof(TViewOperator), out object? dictValue)) {
//            var tcs = (TaskCompletionSource<TViewOperator>)dictValue;
//            operatorTaskCompletions.Remove(typeof(TViewOperator));
//            existingOperators[typeof(TViewOperator)] = viewOperator!;
//            tcs.SetResult(viewOperator);
//        }
//        else {
//            existingOperators[typeof(TViewOperator)] = viewOperator!;
//        }
//    }

//    public static void ReleaseOperator<TViewOperator>() => existingOperators.Remove(typeof(TViewOperator));

//    static Task<TViewOperator> GetViewOperatorCore<TViewOperator>(Action openViewAction) {
//        if (existingOperators.TryGetValue(typeof(TViewOperator), out object existingOperator)) {
//            return Task.FromResult((TViewOperator)existingOperator);
//        }
//        var tcs = new TaskCompletionSource<TViewOperator>();
//        openViewAction();
//        operatorTaskCompletions[typeof(TViewOperator)] = tcs;
//        return tcs.Task;
//    }

//    [ProxyMethod("Navigates to the Customers view")]
//    public static Task<CustomersViewOperator> GetCustomersViewOperatorAsync() {
//        Console.WriteLine("GetCustomersViewOperatorAsync called (host)");
//        return Task.FromResult(new CustomersViewOperator(null!, null!));
//    }

//    [ProxyMethod("Navigates to the Scheduler view")]
//    public static Task<SchedulerViewOperator> GetSchedulerViewOperatorAsync() {
//        Console.WriteLine("GetSchedulerViewOperatorAsync called (host)");
//        return Task.FromResult(new SchedulerViewOperator(null!));
//    }

//    [ProxyMethod("Navigates to the Emails view")]
//    public static Task<EmailsViewOperator> GetEmailsViewOperatorAsync() {
//        Console.WriteLine("GetSchedulerViewOperatorAsync called (host)");
//        return Task.FromResult(new EmailsViewOperator());
//    }
//}

//[ProxyClass(description: "Manages the Customer View")]
//public class CustomersViewOperator {
//    ViewModels.DataGridViewModel? ViewModel;
//    object? Grid;
//    public CustomersViewOperator(ViewModels.DataGridViewModel? viewModel, object? grid) {
//        ViewModel = viewModel;
//        Grid = grid;
//    }

//    [ProxyMethod]
//    public void Test() => Console.WriteLine("CustomersViewOperator.Test called (host)");

//    [ProxyMethod]
//    public void ShowColumnChooser() => Console.WriteLine("CustomersViewOperator.ShowColumnChooser called (host)");

//    [ProxyMethod]
//    public void AddCustomer(Customer customer) {
//        Console.WriteLine($"Adding customer {customer.Name}, {customer.Company} (Id={customer.Id})");
//    }

//    [ProxyMethod]
//    public async Task<Customer> GetCustomer() {
//        await Task.Delay(50);
//        return new Customer { Id = 4, Name = "Brad", Company = "Contoso" };
//    }
//}


