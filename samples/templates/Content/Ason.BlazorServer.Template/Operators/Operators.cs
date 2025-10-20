using Ason;
using Ason.BlazorServer.Template.Components.Pages;
using Ason.BlazorServer.Template.Model;
using Ason.BlazorServer.Template.State;

namespace Ason.BlazorServer.Template.Operators;


[AsonOperator]
public class MainAppOperator : RootOperator<SessionState> {
    public MainAppOperator(SessionState attachedObject) : base(attachedObject) { }

    [AsonMethod]
    public async Task<CustomersViewOperator> GetCustomersViewOperatorAsync() =>
        await GetViewOperator<CustomersViewOperator>(() => AttachedObject.Nav.NavigateTo("/customers"));

    [AsonMethod]
    public async Task<OrdersViewOperator> GetOrdersViewOperatorAsync() =>
        await GetViewOperator<OrdersViewOperator>(() => AttachedObject.Nav.NavigateTo("/orders"));
}

[AsonOperator]
public class CustomersViewOperator : OperatorBase<Customers> {

    [AsonMethod("Returns a list of existing customers")]
    public IEnumerable<Customer> GetCustomers() => AttachedObject?.CustomersSource!;

    [AsonMethod]
    public void AddCustomer(Customer customer) => AttachedObject?.AddCustomer(customer);

    [AsonMethod]
    public void EditCustomer(Customer customer) => AttachedObject?.EditCustomer(customer);

    [AsonMethod]
    public void DeleteCustomer(int customerId) => AttachedObject?.DeleteCustomer(customerId);
}

[AsonOperator]
public class OrdersViewOperator : OperatorBase<Orders> {

    [AsonMethod("Returns a list of all orders")]
    public IEnumerable<Order> GetOrders() => AttachedObject?.OrdersSource!;

    [AsonMethod]
    public void AddOrder(Order order) => AttachedObject?.AddOrder(order);

    [AsonMethod]
    public void EditOrder(Order order) => AttachedObject?.EditOrder(order);

    [AsonMethod]
    public void DeleteOrder(int orderId) => AttachedObject?.DeleteOrder(orderId);
}

