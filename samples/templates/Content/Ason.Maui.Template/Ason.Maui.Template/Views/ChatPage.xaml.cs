using Ason;
using Ason.Maui.Template.ViewModels;

namespace Ason.Maui.Template.Views;

public partial class ChatPage : ContentPage
{
	public ChatPage(RootOperator rootOperator)
	{
		InitializeComponent();
		this.BindingContext = new ChatViewModel(rootOperator);
	}
}