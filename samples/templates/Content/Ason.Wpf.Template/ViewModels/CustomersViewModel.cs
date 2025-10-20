using Ason;
using Ason.Wpf.Template.Operators;
using Ason.Wpf.Template.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Ason.Wpf.Template.ViewModels;
public partial class CustomersViewModel : ObservableObject {

    [ObservableProperty]
    ObservableCollection<Customer> customers = new ObservableCollection<Customer>() {
        new Customer { Id = 1, Name = "Alice Johnson", Email = "alice@example.com" },
        new Customer { Id = 2, Name = "Bob Smith", Email = "bob.smith@ason.com" },
        new Customer { Id = 3, Name = "Carol Davis", Email = "carol.davis@example.com" }
    };
    public CustomersViewModel(RootOperator rootOperator) {
        //IMPORTANT: Attach the operator to the view instance
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
