using Ason;
using Ason.WinForms.Template.Operators;
using Ason.WinForms.Template.Views;

namespace Ason.WinForms.Template {
    public partial class MainForm : Form {
        RootOperator rootOperator;
        CustomersView? customersView;
        OrdersView? ordersView;
        CustomersView CustomersView => customersView ??= new CustomersView(rootOperator);
        OrdersView OrdersView => ordersView ??= new OrdersView(rootOperator);

        bool _suppressNavigationEvent; // prevents recursive NavigateTo calls when we programmatically change selection

        public MainForm() {
            InitializeComponent();
            rootOperator = new MainAppOperator(this);
            navigationListBox.Items.Add("Customers");
            navigationListBox.Items.Add("Orders");
            navigationListBox.SelectedIndex = 0;
            AddChat();
        }

        void AddChat() {
            ChatView chatView1 = new ChatView(rootOperator);
            chatView1.Dock = DockStyle.Right;
            chatView1.Location = new Point(802, 4);
            chatView1.Size = new Size(293, 499);
            chatView1.TabIndex = 2;
            this.Controls.Add(chatView1);
        }

        private void navigationListBox_SelectedValueChanged(object sender, EventArgs e) {
            if (_suppressNavigationEvent) return;
            if (navigationListBox.SelectedItem is not string viewName)
                return;
            NavigateTo(viewName);
        }

        public void NavigateTo(string name) {
            int idx = navigationListBox.Items.IndexOf(name);
            if (idx >= 0 && navigationListBox.SelectedIndex != idx) {
                _suppressNavigationEvent = true;
                navigationListBox.SelectedIndex = idx;
                _suppressNavigationEvent = false;
            }

            UserControl page = name switch {
                "Customers" => CustomersView,
                "Orders" => OrdersView,
                _ => throw new NotImplementedException()
            };
            page.Dock = DockStyle.Fill;
            hostPanel.Controls.Clear();
            hostPanel.Controls.Add(page);
        }
    }
}
