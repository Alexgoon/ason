using Ason;
using Ason.CodeGen;
using AsonRunner;
using Ason.WinForms.Template.Operators;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Ason.WinForms.Template.Views {
    public partial class ChatView : UserControl {
        AsonClient asonChatClient;
        public ChatView(RootOperator rootOperator) {
            InitializeComponent();

            var apiKey = Environment.GetEnvironmentVariable("MY_OPEN_AI_KEY") ?? string.Empty;
            IChatCompletionService chatService = new OpenAIChatCompletionService(modelId: "gpt-4.1-mini", apiKey: apiKey);

            OperatorsLibrary operatorLibrary = new OperatorBuilder()
                                                    .AddAssemblies(typeof(MainAppOperator).Assembly)
                                                    .Build();

            AsonClientOptions options = new() {
                ExecutionMode = ExecutionMode.ExternalProcess,
            };
            asonChatClient = new AsonClient(chatService, rootOperator, operatorLibrary, options);


            inputTextBox.Text = @"Update the contact details for the following customers:
- Alice Johnson:  
  Phone: +1 (212) 555-0147  
  Address: 123 Main St, New York, NY  
- Bob Smith:  
  Phone: +1 (310) 555-0923  
  Address: 456 Oak Ave, Los Angeles, CA  
- Carol Davis:  
  Phone: +1 (617) 555-3789  
  Address: 789 Pine Rd, Boston, MA";
        }

        private async void sendButton_Click(object sender, EventArgs e) {
            await SendCurrentMessageAsync();
        }

        private async Task SendCurrentMessageAsync() {
            var userText = inputTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(userText)) return;
            inputTextBox.Text = string.Empty;
            responseLabel.Text = string.Empty;
            inputPanel.Enabled = false;

            await foreach (var token in asonChatClient.SendStreamingAsync(userText)) {
                responseLabel.Text += token;
            }

            inputPanel.Enabled = true;
        }

        private async void inputTextBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter && !e.Control && !e.Shift) {
                e.SuppressKeyPress = true;
                await SendCurrentMessageAsync();
            }
        }
    }
}