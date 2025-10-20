using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ason;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using WpfSampleApp.AI;
using WpfSampleApp.Model;

namespace WpfSampleApp.ViewModels;

public partial class EmployeeEditViewModel : ObservableObject {
    public Employee OriginalEmployee { get; private set; }

    // Callback invoked after a successful save so parent VM can replace the item in the collection
    public Action<Employee, Employee>? OnSaved { get; set; }

    [ObservableProperty]
    Employee editable; // working copy

    [ObservableProperty]
    Sale? selectedSale;

    OperatorBase rootOperator;

    public ObservableCollection<Sale> SalesCollection { get; }

    public EmployeeEditViewModel(Employee original, RootOperator rootOperator) {
        OriginalEmployee = original;
        Editable = CloneEmployee(original);
        SalesCollection = new ObservableCollection<Sale>(Editable.Sales);
        this.rootOperator = rootOperator;

        rootOperator.AttachChildOperator<EmployeeEditViewOperator>(this, original.Id.ToString());
    }

    static Employee CloneEmployee(Employee source) => new() {
        Id = source.Id,
        FirstName = source.FirstName,
        LastName = source.LastName,
        Email = source.Email,
        Position = source.Position,
        HireDate = source.HireDate,
        Sales = source.Sales?.Select(s => new Sale {
            Id = s.Id,
            ProductName = s.ProductName,
            Quantity = s.Quantity,
            Price = s.Price,
            SaleDate = s.SaleDate
        }).ToList() ?? new List<Sale>()
    };

    [RelayCommand]
    public void AddSale() {
        var nextId = (SalesCollection.Count == 0 ? 1 : SalesCollection.Max(s => s.Id) + 1);
        var sale = new Sale {
            Id = nextId,
            ProductName = "New Product",
            Quantity = 1,
            Price = 0m,
            SaleDate = DateTime.Today
        };
        SalesCollection.Add(sale);
        SelectedSale = sale;
    }

    [RelayCommand]
    public void DeleteSale() {
        if (SelectedSale != null) {
            SalesCollection.Remove(SelectedSale);
            SelectedSale = null;
        }
    }

    [RelayCommand]
    public void Save() {
        var old = OriginalEmployee;
        var updated = new Employee {
            Id = Editable.Id,
            FirstName = Editable.FirstName,
            LastName = Editable.LastName,
            Email = Editable.Email,
            Position = Editable.Position,
            HireDate = Editable.HireDate,
            Sales = SalesCollection.Select(s => new Sale {
                Id = s.Id,
                ProductName = s.ProductName,
                Quantity = s.Quantity,
                Price = s.Price,
                SaleDate = s.SaleDate
            }).ToList()
        };
        OriginalEmployee = updated; // update reference for future cancels
        OnSaved?.Invoke(old, updated);
    }

    [RelayCommand]
    public void Cancel() {
        // discard by restoring Editable from current OriginalEmployee
        Editable = CloneEmployee(OriginalEmployee);
        SalesCollection.Clear();
        foreach (var s in Editable.Sales)
            SalesCollection.Add(s);
        SelectedSale = null;
    }
}
