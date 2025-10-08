using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace WpfSampleApp.Views;

/// <summary>
/// Interaction logic for ChatView.xaml
/// </summary>
public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
    }
}


public class NullToVisiblityConverer : IValueConverter {
    public bool Invert { get; set; } = false;
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (Invert) {
            return value is null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        return value is null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}