using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace WpfSampleApp.Views;

/// <summary>
/// Interaction logic for CalenderView.xaml
/// </summary>
public partial class CalenderView : UserControl
{
    public CalenderView()
    {
        InitializeComponent();
    }
}

public class DateOnlyConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is DateTime dt)
            return dt.Date;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
