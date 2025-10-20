namespace Ason.WinForms.Template.Views {
    partial class OrdersView {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            ordersDataGrid = new DataGridView();
            ((System.ComponentModel.ISupportInitialize)ordersDataGrid).BeginInit();
            SuspendLayout();
            // 
            // ordersDataGrid
            // 
            ordersDataGrid.BorderStyle = BorderStyle.None;
            ordersDataGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            ordersDataGrid.Dock = DockStyle.Fill;
            ordersDataGrid.Location = new Point(4, 0);
            ordersDataGrid.Name = "ordersDataGrid";
            ordersDataGrid.Size = new Size(142, 150);
            ordersDataGrid.TabIndex = 0;
            // 
            // OrdersView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(ordersDataGrid);
            Name = "OrdersView";
            Padding = new Padding(4, 0, 4, 0);
            Load += OrdersView_Load;
            ((System.ComponentModel.ISupportInitialize)ordersDataGrid).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private DataGridView ordersDataGrid;
    }
}
