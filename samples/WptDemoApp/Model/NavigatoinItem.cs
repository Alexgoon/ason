using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfSampleApp.Model; 
public class NavigatoinItem {
    public string Name { get; init; }
    object? viewModel;
    public object ViewModel {
        get {
            if (viewModel == null) {
                viewModel = _viewModelFactory();
            }
            return viewModel;
        }
    }
    readonly Func<object> _viewModelFactory;
    public NavigatoinItem(string name,  Func<object> viewModelFactory) {
        Name = name;
        _viewModelFactory = viewModelFactory;
    }
}
