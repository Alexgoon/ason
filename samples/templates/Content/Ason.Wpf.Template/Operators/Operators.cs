using Ason;
using Ason.Wpf.Template.Model;
using Ason.Wpf.Template.ViewModels;

namespace Ason.Wpf.Template.Operators;

[AsonOperator]
public class MainAppOperator : RootOperator<MainViewModel> {
    public MainAppOperator(MainViewModel attachedObject) : base(attachedObject) { }

    [AsonMethod]
    public async Task<CustomersViewOperator> GetCustomersViewOperatorAsync() =>
        await GetViewOperator<CustomersViewOperator>(AttachedObject.Navigate<CustomersViewModel>);

    [AsonMethod]
    public async Task<OrdersViewOperator> GetOrdersViewOperatorAsync() =>
        await GetViewOperator<OrdersViewOperator>(AttachedObject.Navigate<OrdersViewModel>);
}

[AsonOperator]
public class CustomersViewOperator : OperatorBase<CustomersViewModel> {

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
public class OrdersViewOperator : OperatorBase<OrdersViewModel> {

    [AsonMethod("Returns a list of all orders")]
    public IEnumerable<Order> GetOrders() => AttachedObject?.Orders!;

    [AsonMethod]
    public void AddOrder(Order order) => AttachedObject?.AddOrder(order);

    [AsonMethod]
    public void EditOrder(Order order) => AttachedObject?.EditOrder(order);

    [AsonMethod]
    public void DeleteOrder(int orderId) => AttachedObject?.DeleteOrder(orderId);
}
