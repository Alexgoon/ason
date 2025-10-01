using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ason;
using System.Collections.ObjectModel;
using WpfSampleApp.AI;
using WpfSampleApp.Model;
using System.Linq;

namespace WpfSampleApp.ViewModels;

public partial class EmployeesViewModel(RootOperator rootOperator) : ObservableObject {
    [ObservableProperty]
    ObservableCollection<Employee> employees = new();

    [ObservableProperty]
    ObservableCollection<EmployeeEditViewModel> editedEmployeeViewModels = new();

    [ObservableProperty]
    EmployeeEditViewModel? selectedEditedEmployeeViewModel;

    [ObservableProperty]
    Employee? selectedEmployee;

    bool isDataLoaded;

    [RelayCommand]
    async public Task LoadDataAsync() {
        if (isDataLoaded)
            return;
        Employees = new ObservableCollection<Employee>(await FakeDataService.GetEmployeesAsync());
        rootOperator.AttachChildOperator<EmployeesViewOperator>(this);
        isDataLoaded = true;
    }

    [RelayCommand]
    public void EditEmployee(int? employeeId = null) {
        Employee? employeeToEdit = null;
        if (employeeId == null)
            employeeToEdit = SelectedEmployee;
        else
            employeeToEdit = Employees.FirstOrDefault(emp => emp.Id == employeeId);
        if (employeeToEdit == null)
            return;
        if (!EditedEmployeeViewModels.Any(vm => vm.OriginalEmployee.Id == employeeToEdit.Id)) {
            var vm = new EmployeeEditViewModel(employeeToEdit, rootOperator) {
                OnSaved = (oldEmp, newEmp) => ReplaceEmployee(oldEmp, newEmp)
            };
            EditedEmployeeViewModels.Add(vm);
            SelectedEditedEmployeeViewModel = vm;
        }
    }

    void ReplaceEmployee(Employee oldEmp, Employee newEmp) {
        var idx = Employees.IndexOf(oldEmp);
        if (idx >= 0) {
            Employees[idx] = newEmp; // triggers collection change so DataGrid refreshes
            if (SelectedEmployee == oldEmp)
                SelectedEmployee = newEmp;
        }
    }

    [RelayCommand]
    public void AddEmployee() {
        var newEmployee = new Employee {
            Id = (Employees?.Max(e => e.Id) ?? 0) + 1,
            FirstName = "New",
            LastName = "Employee",
            Email = "new.employee@contoso-crm.com",
            Position = "New Position",
            HireDate = System.DateTime.Today,
            Sales = new List<Sale>()
        };
        Employees!.Add(newEmployee);
        SelectedEmployee = newEmployee;
        EditEmployee();
    }

    [RelayCommand]
    public void DeleteEmployee() {
        if (SelectedEmployee == null)
            return;
        var editors = EditedEmployeeViewModels.Where(vm => vm.OriginalEmployee == SelectedEmployee).ToList();
        foreach (var ed in editors)
            EditedEmployeeViewModels.Remove(ed);
        Employees.Remove(SelectedEmployee);
        SelectedEmployee = null;
    }

    [RelayCommand]
    public void CloseEditor(EmployeeEditViewModel vm) {
        if (vm != null && EditedEmployeeViewModels.Contains(vm)) {
            EditedEmployeeViewModels.Remove(vm);
        }
    }

    [RelayCommand]
    public void SaveAll() {
        // Save each editor, then close all tabs
        var editorsSnapshot = EditedEmployeeViewModels.ToList();
        foreach (var vm in editorsSnapshot) {
            vm.Save();
        }
        EditedEmployeeViewModels.Clear();
    }
}
