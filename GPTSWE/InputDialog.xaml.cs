using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GPTSWE
{
    public partial class InputDialog : Window
    {
        public event Action<string, string> ResponseGenerated;
        public string UserInput { get; private set; }

        public InputDialog()
        {
            InitializeComponent();
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string userInput = UserInputTextBox.Text;

            // Generate a response (in this case, always "Hello World")
            string response = "Hello World";

            // Display the response
            ResponseTextBox.Text = response;

            // Clear the input textbox if desired
            UserInputTextBox.Clear();
            UserInputTextBox.Focus();

            // Raise the event to notify subscribers
            ResponseGenerated.Invoke(userInput, response);
        }
    }
}
