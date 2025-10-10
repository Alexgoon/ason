using Ason;
using Ason.Wpf.Template.Operators;
using Ason.Wpf.Template.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Ason.Wpf.Template.ViewModels; 
public partial class OrdersViewModel : ObservableObject {

    [ObservableProperty]
    ObservableCollection<Order> orders = new ObservableCollection<Order>() {
        new Order { OrderId = 1001, Customer = new Customer { Id = 1, Name = "Alice Johnson", Email = "alice@example.com" }, OrderDate = new DateTime(2025, 1, 15), TotalAmount = 250.75m },
        new Order { OrderId = 1002, Customer = new Customer { Id = 2, Name = "Bob Smith", Email = "bob.smith@example.com" }, OrderDate = new DateTime(2025, 2, 3), TotalAmount = 99.99m },
        new Order { OrderId = 1003, Customer = new Customer { Id = 3, Name = "Carol Davis", Email = "carol.davis@example.com" }, OrderDate = new DateTime(2025, 3, 21), TotalAmount = 560.40m }
    };
    public OrdersViewModel(RootOperator rootOperator) {
        //IMPORTANT: Attach the operator to the view instance
        rootOperator.AttachChildOperator<OrdersViewOperator>(this);
    }

    public void AddOrder(Order newOrder) {
        Orders.Add(newOrder);
    }

    public void DeleteOrder(int orderId) {
        var order = Orders.FirstOrDefault(o => o.OrderId == orderId);
        if (order != null)
            Orders.Remove(order);
    }

    public void EditOrder(Order updatedOrder) {
        var oldOrder = Orders.FirstOrDefault(c => c.OrderId == updatedOrder.OrderId);
        if (oldOrder != null) {
            var idx = Orders.IndexOf(oldOrder);
            Orders[idx] = updatedOrder;
        }
    }
}
