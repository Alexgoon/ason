using Ason;
using Ason.WinForms.Template.Operators;
using Ason.WinForms.Template.Model;
using System.ComponentModel;

namespace Ason.WinForms.Template.Views;
public partial class OrdersView : UserControl {
    public BindingList<Order> Orders = new BindingList<Order>() {
            new Order { OrderId = 1001, Customer = new Customer { Id = 1, Name = "Alice Johnson", Email = "alice@example.com" }, OrderDate = new DateTime(2025, 1, 15), TotalAmount = 250.75m },
            new Order { OrderId = 1002, Customer = new Customer { Id = 2, Name = "Bob Smith", Email = "bob.smith@example.com" }, OrderDate = new DateTime(2025, 2, 3), TotalAmount = 99.99m },
            new Order { OrderId = 1003, Customer = new Customer { Id = 3, Name = "Carol Davis", Email = "carol.davis@example.com" }, OrderDate = new DateTime(2025, 3, 21), TotalAmount = 560.40m }
        };

    RootOperator _rootOperator;

    public OrdersView(RootOperator rootOperator) {
        InitializeComponent();
        _rootOperator = rootOperator;
        ordersDataGrid.DataSource = Orders;
    }

    private void OrdersView_Load(object sender, EventArgs e) {
        _rootOperator.AttachChildOperator<OrdersViewOperator>(this);
    }

    public void AddOrder(Order newOrder) {
        Orders.Add(newOrder);
    }

    public void DeleteOrder(int orderId) {
        var order = Orders.FirstOrDefault(o => o.OrderId == orderId);
        if (order == null)
            return;
        Orders.Remove(order);
    }

    public void EditOrder(Order updatedOrder) {
        var oldOrder = Orders.FirstOrDefault(c => c.OrderId == updatedOrder.OrderId);
        if (oldOrder == null)
            return;
        var idx = Orders.IndexOf(oldOrder);
        Orders[idx] = updatedOrder;
    }
}
