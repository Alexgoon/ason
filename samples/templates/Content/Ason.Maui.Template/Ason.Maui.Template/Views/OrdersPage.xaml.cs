using Ason;
using Ason.Maui.Template.ViewModels;

namespace Ason.Maui.Template.Views;

public partial class OrdersPage : ContentPage
{
	public OrdersPage(RootOperator rootOperator)
	{
		InitializeComponent();
        this.BindingContext = new OrdersViewModel(rootOperator);
    }
}