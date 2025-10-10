namespace Ason.WinForms.Template.Views {
    partial class CustomersView {
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
            customersDataGrid = new DataGridView();
            ((System.ComponentModel.ISupportInitialize)customersDataGrid).BeginInit();
            SuspendLayout();
            // 
            // customersDataGrid
            // 
            customersDataGrid.BorderStyle = BorderStyle.None;
            customersDataGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            customersDataGrid.Dock = DockStyle.Fill;
            customersDataGrid.Location = new Point(4, 0);
            customersDataGrid.Margin = new Padding(4);
            customersDataGrid.Name = "customersDataGrid";
            customersDataGrid.Size = new Size(142, 150);
            customersDataGrid.TabIndex = 0;
            // 
            // CustomersView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(customersDataGrid);
            Name = "CustomersView";
            Padding = new Padding(4, 0, 4, 0);
            Load += CustomersView_Load;
            ((System.ComponentModel.ISupportInitialize)customersDataGrid).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private DataGridView customersDataGrid;
    }
}
