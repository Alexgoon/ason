using Ason;
using Ason.BlazorServer.Template.Operators;
using Ason.BlazorServer.Template.Model;
using Microsoft.AspNetCore.Components;
using System.Collections.ObjectModel;

namespace Ason.BlazorServer.Template.State;
public class SessionState(NavigationManager nav) 
{
    public List<Customer> Customers { get; } = new List<Customer>() {
        new Customer { Id = 1, Name = "Alice Johnson", Email = "alice@example.com" },
        new Customer { Id = 2, Name = "Bob Smith", Email = "bob.smith@ason.com" },
        new Customer { Id = 3, Name = "Carol Davis", Email = "carol.davis@example.com" }
    };
    public List<Order> Orders { get; } = new List<Order>() {
        new Order { OrderId = 1001, Customer = new Customer { Id = 1, Name = "Alice Johnson", Email = "alice@example.com" }, OrderDate = new DateTime(2025, 1, 15), TotalAmount = 250.75m },
        new Order { OrderId = 1002, Customer = new Customer { Id = 2, Name = "Bob Smith", Email = "bob.smith@example.com" }, OrderDate = new DateTime(2025, 2, 3), TotalAmount = 99.99m },
        new Order { OrderId = 1003, Customer = new Customer { Id = 3, Name = "Carol Davis", Email = "carol.davis@example.com" }, OrderDate = new DateTime(2025, 3, 21), TotalAmount = 560.40m }
    };

    RootOperator? mainAppOperator;
    public RootOperator MainAppOperator => mainAppOperator ??= new MainAppOperator(this);
    public NavigationManager Nav { get; } = nav;
}