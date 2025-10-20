using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WpfSampleApp.Views;

/// <summary>
/// Interaction logic for CustomersView.xaml
/// </summary>
public partial class EmployeesView : UserControl
{
    public EmployeesView()
    {
        InitializeComponent();
    }
}

public class CountToVisibilityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is int count && count > 0)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}