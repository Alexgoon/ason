using Ason;
using Ason.Maui.Template.ViewModels;

namespace Ason.Maui.Template.Views;

public partial class CustomersPage : ContentPage
{
	public CustomersPage(RootOperator rootOperator)
	{
		InitializeComponent();
        this.BindingContext = new CustomersViewModel(rootOperator);
    }
}