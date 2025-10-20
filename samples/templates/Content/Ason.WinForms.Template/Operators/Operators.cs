using Ason;
using Ason.WinForms.Template.Model;
using Ason.WinForms.Template.Views;

namespace Ason.WinForms.Template.Operators;
[AsonOperator]
public class MainAppOperator : RootOperator<MainForm> {
    public MainAppOperator(MainForm attachedObject) : base(attachedObject) { }

    [AsonMethod]
    public async Task<CustomersViewOperator> GetCustomersViewOperatorAsync() =>
        await GetViewOperator<CustomersViewOperator>(() => AttachedObject.NavigateTo("Customers"));

    [AsonMethod]
    public async Task<OrdersViewOperator> GetOrdersViewOperatorAsync() =>
        await GetViewOperator<OrdersViewOperator>(() => AttachedObject.NavigateTo("Orders"));
}

[AsonOperator]
public class CustomersViewOperator : OperatorBase<CustomersView> {

    [AsonMethod("Returns a list of existing customers")]
    public IEnumerable<Customer> GetCustomers() => AttachedObject?.Customers!;

    [AsonMethod]
    public void AddCustomer(Customer customer) => AttachedObject?.AddCustomer(customer);

    [AsonMethod]
    public void EditCustomer(Customer customer) => AttachedObject?.EditCustomer(customer);

    [AsonMethod]
    public void DeleteCustomer(int customerId) => AttachedObject?.DeleteCustomer(customerId);
}

[AsonOperator]
public class OrdersViewOperator : OperatorBase<OrdersView> {

    [AsonMethod("Returns a list of all orders")]
    public IEnumerable<Order> GetOrders() => AttachedObject?.Orders!;

    [AsonMethod]
    public void AddOrder(Order order) => AttachedObject?.AddOrder(order);

    [AsonMethod]
    public void EditOrder(Order order) => AttachedObject?.EditOrder(order);

    [AsonMethod]
    public void DeleteOrder(int orderId) => AttachedObject?.DeleteOrder(orderId);
}
