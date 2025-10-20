using Ason;
using Ason.Console.Template.Model;
using System.Diagnostics;

namespace Ason.Console.Template.Modules;
public class CustomersModule {
    public List<Customer> Customers { get; set; } = new List<Customer>() {
        new Customer { Id = 1, Name = "Alice Johnson", Email = "alice@example.com" },
        new Customer { Id = 2, Name = "Bob Smith", Email = "bob.smith@ason.com" },
        new Customer { Id = 3, Name = "Carol Davis", Email = "carol.davis@example.com" }
    };
    public CustomersModule(RootOperator rootOperator) {
        //IMPORTANT: Attach the operator to the module instance
        rootOperator.AttachChildOperator<CustomersModuleOperator>(this);
    }
    public void AddCustomer(Customer newCustomer) {
        Customers.Add(newCustomer);
        Debug.WriteLine($"Customer {newCustomer.Name} added.");
    }

    public void DeleteCustomer(int customerId) {
        var customer = Customers.FirstOrDefault(c => c.Id == customerId);
        if (customer != null)
            Customers.Remove(customer);
        Debug.WriteLine($"Customer {customerId} deleted.");
    }

    public void EditCustomer(Customer updatedCustomer) {
        var customer = Customers.FirstOrDefault(c => c.Id == updatedCustomer.Id);
        if (customer != null) {
            customer.Name = updatedCustomer.Name;
            customer.Email = updatedCustomer.Email;
            customer.Phone = updatedCustomer.Phone;
            customer.Address = updatedCustomer.Address;
        }
        Debug.WriteLine($"Customer {updatedCustomer.Name} updated.");
    }
}
