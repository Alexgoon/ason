using Ason;
using Ason.WinForms.Template.Operators;
using Ason.WinForms.Template.Model;
using System.ComponentModel;

namespace Ason.WinForms.Template.Views {
    public partial class CustomersView : UserControl {

        public BindingList<Customer> Customers = new BindingList<Customer>() {
                new Customer { Id = 1, Name = "Alice Johnson", Email = "alice@example.com" },
                new Customer { Id = 2, Name = "Bob Smith", Email = "bob.smith@ason.com" },
                new Customer { Id = 3, Name = "Carol Davis", Email = "carol.davis@example.com" }
             };

        RootOperator _rootOperator;

        public CustomersView(RootOperator rootOperator) {
            InitializeComponent();
            _rootOperator = rootOperator;
            customersDataGrid.DataSource = Customers;
        }

        private void CustomersView_Load(object sender, EventArgs e) {
            _rootOperator.AttachChildOperator<CustomersViewOperator>(this);
        }

        public void AddCustomer(Customer newCustomer) {
            Customers.Add(newCustomer);
        }

        public void DeleteCustomer(int customerId) {
            var customer = Customers.FirstOrDefault(c => c.Id == customerId);
            if (customer != null)
                Customers.Remove(customer);
        }

        public void EditCustomer(Customer updatedCustomer) {
            var oldCustomer = Customers.FirstOrDefault(c => c.Id == updatedCustomer.Id);
            if (oldCustomer != null) {
                var idx = Customers.IndexOf(oldCustomer);
                Customers[idx] = updatedCustomer;
            }
        }
    }
}
