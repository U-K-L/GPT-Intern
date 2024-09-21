using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.IO;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;


namespace GPTSWE
{
    public class ResponseEventArgs : EventArgs
    {
        public string UserInput { get; }
        public string Response { get; }

        public ResponseEventArgs(string userInput, string response)
        {
            UserInput = userInput;
            Response = response;
        }
    }

    /// <summary>
    /// Interaction logic for GPTSWEToolWindowControl.
    /// </summary>
    public partial class GPTSWEToolWindowControl : UserControl
    {

        public event Action<string, string> ResponseGenerated;
        public event EventHandler<ResponseEventArgs> ResponseReceived;
        public string UserInput { get; private set; }

        public GPTSWEToolWindowControl()
        {
            this.InitializeComponent();

            CreateAgent();
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessUserInput();

        }


        private void UserInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                ProcessUserInput();
                e.Handled = true; // Prevent the ding sound on Enter
            }
        }

        private void UserInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true; // Prevents adding a new line
                ProcessUserInput();
            }
        }


        public static ChatHistory history;
        public static Kernel kernel;

        private static async Task<AsyncVoidMethodBuilder> CreateAgent()
        {
            string modelId = GPTSWEPackage.MODEL;
            string apiKey = GPTSWEPackage.APIKEY; // Using settings for API key

            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("API key not set in Visual Studio settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new InvalidOperationException("API key not found");
            }

            if (string.IsNullOrEmpty(modelId))
            {
                MessageBox.Show("Model not set in Visual Studio settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new InvalidOperationException("Model not found");
            }

            // Create a kernel with Azure OpenAI chat completion
            var builder = Kernel.CreateBuilder().AddOpenAIChatCompletion(modelId, apiKey);

            kernel = builder.Build();

            // Retrieve the chat completion service
            var chatCompletionService = kernel.Services.GetRequiredService<IChatCompletionService>();

            kernel.Plugins.AddFromType<GPTProgramming>("Programming");

            // Enable planning
            OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new OpenAIPromptExecutionSettings()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };


            // Create a history store the conversation
            history = new ChatHistory(GPTSWEConstants.PersonaText);


            AsyncVoidMethodBuilder n;
            return n;
        }

        private async void ProcessUserInput()
        {
            string userInput = UserInputTextBox.Text.Trim();
            //AddResponseToPanel("Error", fileContent);
            if (!string.IsNullOrEmpty(userInput))
            {

                // Display the user input and response in the ResponsesPanel
                AddResponseToPanel("You", userInput);
                // Clear the input textbox
                UserInputTextBox.Clear();

                try
                {

                    // Add user input
                    history.AddUserMessage(userInput);
                    var chatCompletionService = kernel.Services.GetRequiredService<IChatCompletionService>();

                    // Enable planning
                    OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new OpenAIPromptExecutionSettings()
                    {
                        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                    };

                    // Get the response from the AI
                    var result = await chatCompletionService.GetChatMessageContentAsync(
                        history,
                        executionSettings: openAIPromptExecutionSettings,
                        kernel: kernel);

                    // Add the message from the agent to the chat history
                    history.AddMessage(result.Role, result.Content ?? string.Empty);

                    //AddResponseToPanel("Error", result.);
                    AddResponseToPanel("GPT-Intern", result.Content);


                    UserInputTextBox.Focus();
                }
                catch (Exception ex)
                {
                    //AddResponseToPanel("Error", ex.Message);
                }

            }
        }


        private void AddResponseToPanel(string senderName, string message)
        {
            // Create a TextBox for the message
            TextBox messageBox = new TextBox
            {
                Text = $"{senderName}: {message}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(5, 5, 5, 5),
                FontSize = 14, // Set the desired font size here
                IsReadOnly = true, // Make it read-only
                Background = Brushes.Transparent, // Match background to make it look like a TextBlock
                BorderThickness = new Thickness(0), // Remove the border
                IsReadOnlyCaretVisible = true // Show caret to make it clear text is selectable
            };

            // Optionally style the message
            if (senderName == "You")
            {
                messageBox.Foreground = Brushes.LightBlue;
            }
            else if (senderName == "GPT-Intern")
            {
                messageBox.Foreground = Brushes.LightGreen;
            }
            else if (senderName == "Error")
            {
                messageBox.Foreground = Brushes.PaleVioletRed;
            }

            // Add the message to the panel
            ResponsesPanel.Children.Add(messageBox);

            // Scroll to the bottom
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            if (VisualTreeHelper.GetParent(ResponsesPanel) is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToEnd();
            }
        }
    }
}
