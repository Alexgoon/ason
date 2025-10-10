using Ason;
using Ason.Console.Template.Model;
using Ason.Console.Template.Modules;

namespace Ason.Console.Template;

[AsonOperator]
public class MainOperator : RootOperator<RootModule> {
    public MainOperator(RootModule attachedObject) : base(attachedObject) { }

    [AsonMethod]
    public async Task<CustomersModuleOperator> GetCustomersOperatorAsync() =>
        await GetViewOperator<CustomersModuleOperator>(() => AttachedObject.NavigateTo(nameof(CustomersModule)));


    [AsonMethod]
    public async Task<OrdersModuleOperator> GetOrdersOperatorAsync() =>
        await GetViewOperator<OrdersModuleOperator>(() => AttachedObject.NavigateTo(nameof(OrdersModule)));

}

[AsonOperator]
public class CustomersModuleOperator : OperatorBase<CustomersModule> {

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
public class OrdersModuleOperator : OperatorBase<OrdersModule> {

    [AsonMethod("Returns a list of all orders")]
    public IEnumerable<Order> GetOrders() => AttachedObject?.Orders!;

    [AsonMethod]
    public void AddOrder(Order order) => AttachedObject?.AddOrder(order);

    [AsonMethod]
    public void EditOrder(Order order) => AttachedObject?.EditOrder(order);

    [AsonMethod]
    public void DeleteOrder(int orderId) => AttachedObject?.DeleteOrder(orderId);
}