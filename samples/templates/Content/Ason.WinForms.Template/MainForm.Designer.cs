namespace Ason.WinForms.Template
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            navigationListBox = new ListBox();
            hostPanel = new Panel();
            //chatView1 = new Ason.WinForms.Template.Views.ChatView();
            SuspendLayout();
            // 
            // navigationListBox
            // 
            navigationListBox.Dock = DockStyle.Left;
            navigationListBox.FormattingEnabled = true;
            navigationListBox.Location = new Point(4, 4);
            navigationListBox.Name = "navigationListBox";
            navigationListBox.Size = new Size(175, 499);
            navigationListBox.TabIndex = 0;
            navigationListBox.SelectedValueChanged += navigationListBox_SelectedValueChanged;
            // 
            // hostPanel
            // 
            hostPanel.Dock = DockStyle.Fill;
            hostPanel.Location = new Point(179, 4);
            hostPanel.Name = "hostPanel";
            hostPanel.Size = new Size(623, 499);
            hostPanel.TabIndex = 1;
            // 
            // chatView1
            // 
            //chatView1.Dock = DockStyle.Right;
            //chatView1.Location = new Point(802, 4);
            //chatView1.Name = "chatView1";
            //chatView1.Size = new Size(293, 499);
            //chatView1.TabIndex = 2;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1099, 507);
            Controls.Add(hostPanel);
            Controls.Add(navigationListBox);
            //Controls.Add(chatView1);
            Name = "MainForm";
            Padding = new Padding(4);
            Text = "Form1";
            ResumeLayout(false);
        }

        #endregion

        private ListBox navigationListBox;
        private Panel hostPanel;
        private Views.ChatView chatView1;
    }
}
