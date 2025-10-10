namespace Ason.WinForms.Template.Views {
    partial class ChatView {
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
            responseLabel = new Label();
            inputTextBox = new TextBox();
            sendButton = new Button();
            inputPanel = new Panel();
            inputPanel.SuspendLayout();
            SuspendLayout();
            // 
            // responseLabel
            // 
            responseLabel.BackColor = SystemColors.Window;
            responseLabel.BorderStyle = BorderStyle.FixedSingle;
            responseLabel.Dock = DockStyle.Fill;
            responseLabel.ForeColor = SystemColors.ControlText;
            responseLabel.Location = new Point(0, 0);
            responseLabel.Name = "responseLabel";
            responseLabel.Padding = new Padding(4);
            responseLabel.Size = new Size(293, 311);
            responseLabel.TabIndex = 0;
            // 
            // inputTextBox
            // 
            inputTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            inputTextBox.Location = new Point(4, 4);
            inputTextBox.Multiline = true;
            inputTextBox.Name = "inputTextBox";
            inputTextBox.Size = new Size(246, 70);
            inputTextBox.TabIndex = 0;
            inputTextBox.KeyDown += inputTextBox_KeyDown;
            // 
            // sendButton
            // 
            sendButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            sendButton.Font = new Font("Segoe UI", 12F);
            sendButton.Location = new Point(252, 41);
            sendButton.Name = "sendButton";
            sendButton.Size = new Size(34, 34);
            sendButton.TabIndex = 1;
            sendButton.Text = "➤";
            sendButton.UseVisualStyleBackColor = true;
            sendButton.Click += sendButton_Click;
            // 
            // inputPanel
            // 
            inputPanel.Controls.Add(inputTextBox);
            inputPanel.Controls.Add(sendButton);
            inputPanel.Dock = DockStyle.Bottom;
            inputPanel.Location = new Point(0, 311);
            inputPanel.Name = "inputPanel";
            inputPanel.Padding = new Padding(4);
            inputPanel.Size = new Size(293, 78);
            inputPanel.TabIndex = 1;
            // 
            // ChatView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(responseLabel);
            Controls.Add(inputPanel);
            Name = "ChatView";
            Size = new Size(293, 389);
            inputPanel.ResumeLayout(false);
            inputPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Label responseLabel;
        private TextBox inputTextBox;
        private Button sendButton;
        private Panel inputPanel;
    }
}
