using Ason;
using BlazorAdvancedApp.AI;
using BlazorAdvancedApp.Models;

namespace BlazorAdvancedApp.Models;

public class EmployeeEditModel {
    public event Action? StateChanged;
    public Employee Original { get; }
    public Employee Editable { get; set; }
    public bool IsNew { get; }
    public Sale? SelectedSale { get; set; }
    public IList<Employee> SourceCollection { get; init; } = [];

    public EmployeeEditModel(Employee original, bool isNew, IList<Employee> sourceCollection, RootOperator rootOperator) {
        Original = original;
        IsNew = isNew;
        SourceCollection = sourceCollection;
        Editable = Clone(original);
        rootOperator.AttachChildOperator<EmployeeEditViewOperator>(this, Editable.Id.ToString());
    }

    public void ReplaceEditable(Employee updated) {
        Editable = updated;
        StateChanged?.Invoke();
    }

    Employee Clone(Employee e) => new Employee {
        Id = e.Id,
        FirstName = e.FirstName,
        LastName = e.LastName,
        Email = e.Email,
        Position = e.Position,
        HireDate = e.HireDate,
        Sales = e.Sales.Select(s => new Sale {
            Id = s.Id,
            ProductName = s.ProductName,
            Quantity = s.Quantity,
            Price = s.Price,
            SaleDate = s.SaleDate
        }).ToList()
    };

    public void Apply() {
        Original.FirstName = Editable.FirstName;
        Original.LastName = Editable.LastName;
        Original.Email = Editable.Email;
        Original.Position = Editable.Position;
        Original.HireDate = Editable.HireDate;
        Original.Sales = Editable.Sales;
        if(IsNew && !SourceCollection.Contains(Original)) {
            SourceCollection.Add(Original);
        }
    }
}
