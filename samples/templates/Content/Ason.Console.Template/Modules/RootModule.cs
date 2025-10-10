using Ason;

namespace Ason.Console.Template.Modules; 
public class RootModule {
    public RootOperator RootOperator { get; init; }
    CustomersModule? customersModule;
    OrdersModule? ordersModule;

    public RootModule() {
        RootOperator = new MainOperator(this);
    }
    public void NavigateTo(string moduleName) {
        switch (moduleName) {
            case nameof(CustomersModule):
                customersModule ??= new CustomersModule(RootOperator);
                break;
            case nameof(OrdersModule):
                ordersModule ??= new OrdersModule(RootOperator);
                break;
            default:
                Console.WriteLine($"Unknown module: {moduleName}");
                break;
        }
    }
}
