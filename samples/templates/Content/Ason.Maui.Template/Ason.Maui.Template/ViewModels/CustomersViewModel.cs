using Ason;
using Ason.Maui.Template.Operators;
using Ason.Maui.Template.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Ason.Maui.Template.ViewModels;
public partial class CustomersViewModel(RootOperator rootOperator) : ObservableObject {

    [ObservableProperty]
    ObservableCollection<Customer> customers = new ObservableCollection<Customer>() {
        new Customer { Id = 1, Name = "Alice Johnson", Email = "alice@example.com" },
        new Customer { Id = 2, Name = "Bob Smith", Email = "bob.smith@ason.com" },
        new Customer { Id = 3, Name = "Carol Davis", Email = "carol.davis@example.com" }
    };

    [RelayCommand]
    void Initialize() {
        //IMPORTANT: Attach the operator to the view model instance
        rootOperator.AttachChildOperator<CustomersViewOperator>(this);
    }
    public void AddCustomer(Customer newCustomer) {
        Customers.Add(newCustomer);
    }

    public void DeleteCustomer(int customerId) {
        var customer = Customers.FirstOrDefault(c => c.Id == customerId);
        if (customer != null)
            Customers.Remove(customer);
    }

    public void EditCustomer(Customer updatedCustomer) {
        var oldCustomer = Customers.FirstOrDefault(c => c.Id == updatedCustomer.Id);
        if (oldCustomer != null) {
            var idx = Customers.IndexOf(oldCustomer);
            Customers[idx] = updatedCustomer;
        }
    }
}
