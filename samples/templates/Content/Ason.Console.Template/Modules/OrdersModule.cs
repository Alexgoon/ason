using Ason;
using Ason.Console.Template.Model;
using System.Diagnostics;

namespace Ason.Console.Template.Modules; 
public class OrdersModule {
    public List<Order> Orders { get; set; } = new List<Order>() {
        new Order { OrderId = 1001, Customer = new Customer { Id = 1, Name = "Alice Johnson", Email = "alice@example.com" }, OrderDate = new DateTime(2025, 1, 15), TotalAmount = 250.75m },
        new Order { OrderId = 1002, Customer = new Customer { Id = 2, Name = "Bob Smith", Email = "bob.smith@example.com" }, OrderDate = new DateTime(2025, 2, 3), TotalAmount = 99.99m },
        new Order { OrderId = 1003, Customer = new Customer { Id = 3, Name = "Carol Davis", Email = "carol.davis@example.com" }, OrderDate = new DateTime(2025, 3, 21), TotalAmount = 560.40m }
    }; 
    public OrdersModule(RootOperator rootOperator) {
        //IMPORTANT: Attach the operator to the module instance
        rootOperator.AttachChildOperator<OrdersModuleOperator>(this);
    }

    public void AddOrder(Order newOrder) {
        Orders.Add(newOrder);
        Debug.WriteLine($"Order {newOrder.OrderId}:{newOrder.OrderDate} added.");
    }

    public void DeleteOrder(int orderId) {
        var order = Orders.FirstOrDefault(o => o.OrderId == orderId);
        if (order != null)
            Orders.Remove(order);
        Debug.WriteLine($"Order {orderId} deleted.");
    }

    public void EditOrder(Order newOrder) {
        var order = Orders.FirstOrDefault(o => o.OrderId == newOrder.OrderId);
        if (order != null) {
            order.OrderDate = newOrder.OrderDate;
            order.TotalAmount = newOrder.TotalAmount;
            order.Customer = newOrder.Customer;
        }
        Debug.WriteLine($"Order {newOrder.OrderId}:{newOrder.OrderDate} updated.");
    }
}
