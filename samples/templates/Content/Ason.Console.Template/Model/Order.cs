
using Ason;

namespace Ason.Console.Template.Model;

[AsonModel]
public class Order {
    public int OrderId { get; set; }
    public Customer Customer { get; set; } = new Customer();
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
}
