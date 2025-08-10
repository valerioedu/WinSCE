using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI;

namespace SCE2
{
    public sealed partial class MainWindow : Window
    {
        private bool isAIChatPanelVisible = false;
        private bool isResizingAIChat = false;

        private void AIChat_Click(object sender, RoutedEventArgs e)
        {
            ToggleAIChatPanel();
        }

        private void ToggleAIChatPanel()
        {
            isAIChatPanelVisible = !isAIChatPanelVisible;

            if (isAIChatPanelVisible)
            {
                AIChatColumn.Width = new GridLength(350);
                AIChatPanel.Visibility = Visibility.Visible;
                AIChatSplitter.Visibility = Visibility.Visible;

                AIChatControlPanel.SetParentWindow(this);
                AIChatControlPanel.CloseRequested += AIChatControl_CloseRequested;
            }
            else
            {
                AIChatColumn.Width = new GridLength(0);
                AIChatPanel.Visibility = Visibility.Collapsed;
                AIChatSplitter.Visibility = Visibility.Collapsed;
            }
        }

        private void AIChatControl_CloseRequested(object sender, EventArgs e)
        {
            ToggleAIChatPanel();
        }
        
        private void AIChatSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var splitter = sender as Border;
            splitter?.CapturePointer(e.Pointer);
            isResizingAIChat = true;
        }

        private void AIChatSplitter_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (isResizingAIChat && sender is Border splitter)
            {
                var position = e.GetCurrentPoint(splitter.Parent as UIElement);
                var parentWidth = (splitter.Parent as Grid)?.ActualWidth ?? 0;

                var newWidth = Math.Max(250, Math.Min(600, parentWidth - position.Position.X));
                AIChatColumn.Width = new GridLength(newWidth);
            }
        }

        private void AIChatSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var splitter = sender as Border;
            splitter?.ReleasePointerCapture(e.Pointer);
            isResizingAIChat = false;
        }

        public string GetSelectedCode()
        {
            try
            {
                var selectedText = CodeEditor.SelectedText;
                if (!string.IsNullOrEmpty(selectedText))
                {
                    return selectedText;
                }

                var allText = CodeEditor.Text;
                return allText.Length > 10000 ? allText.Substring(0, 10000) + "\n\n... (truncated)" : allText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting selected code: {ex.Message}");
                return CodeEditor.Text ?? "";
            }
        }
    }

    public sealed partial class AIChatControl : UserControl
    {
        private MainWindow parentWindow;
        private bool isConnected = false;
        private string currentModel = "";
        private string apiKey = "";
        private string OpenAIKey = "";
        private string AnthropicKey = "";
        private List<ChatMessage> chatHistory = new List<ChatMessage>();

        public event EventHandler CloseRequested;

        public AIChatControl()
        {
            this.InitializeComponent();
            LoadSavedCredentials();

            MessageInput.TextChanged += (s, e) =>
            {
                SendButton.IsEnabled = !string.IsNullOrWhiteSpace(MessageInput.Text) && isConnected;
            };
        }

        public void SetParentWindow(MainWindow parent)
        {
            parentWindow = parent;
        }

        private void LoadSavedCredentials()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                var savedOpenAIKey = localSettings.Values["OpenAI_ApiKey"] as string;
                var savedClaudeKey = localSettings.Values["Claude_ApiKey"] as string;
                var savedModel = localSettings.Values["AI_Model"] as string;
                var savedApiKey = localSettings.Values["AI_ApiKey"] as string;

                if (!string.IsNullOrEmpty(savedClaudeKey))
                {
                    ClaudeApiKeyInput.Text = savedClaudeKey;
                }

                if (!string.IsNullOrEmpty(savedOpenAIKey))
                {
                    OpenAiApiKeyInput.Text = savedOpenAIKey;
                }

                if (!string.IsNullOrEmpty(savedModel))
                {
                    var item = ModelSelector.Items.Cast<ComboBoxItem>()
                        .FirstOrDefault(i => i.Tag.ToString() == savedModel);
                    if (item != null)
                    {
                        ModelSelector.SelectedItem = item;
                    }
                }

                if (!string.IsNullOrEmpty(savedApiKey))
                {
                    ApiKeyBox.Password = savedApiKey;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading credentials: {ex.Message}");
            }
        }

        private void SaveCredentials()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["AI_Model"] = currentModel;
                localSettings.Values["Claude_ApiKey"] = apiKey;
                localSettings.Values["OpenAI_ApiKey"] = apiKey;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving credentials: {ex.Message}");
            }
        }

        private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelSelector.SelectedItem is ComboBoxItem selectedItem)
            {
                currentModel = selectedItem.Tag.ToString();

                switch (currentModel)
                {
                    case "claude-sonnet-4-20250514":
                    case "claude-opus-4-1-20250805":
                        ApiKeyBox.PlaceholderText = "Enter your Anthropic API key";
                        break;
                    case "o3-mini":
                    case "gpt-4.1":
                    case "gpt-4o":
                        ApiKeyBox.PlaceholderText = "Enter your OpenAI API key";
                        break;
                }
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentModel))
            {
                ShowStatus("Please select an AI model.", true);
                return;
            }

            if (string.IsNullOrWhiteSpace(ApiKeyBox.Password))
            {
                ShowStatus("Please enter your API key.", true);
                return;
            }

            apiKey = ApiKeyBox.Password;
            LoginButton.IsEnabled = false;
            LoginButton.Content = "Connecting...";

            try
            {
                bool connectionSuccess = await TestConnection();

                if (connectionSuccess)
                {
                    isConnected = true;
                    SaveCredentials();

                    LoginPanel.Visibility = Visibility.Collapsed;
                    WelcomePanel.Visibility = Visibility.Collapsed;
                    ChatScrollViewer.Visibility = Visibility.Visible;
                    InputPanel.Visibility = Visibility.Visible;

                    AddWelcomeMessage();

                    ShowStatus("Connected successfully!", false);
                    MessageInput.Focus(FocusState.Keyboard);
                }
                else
                {
                    ShowStatus("Failed to connect. Please check your API key.", true);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Connection error: {ex.Message}", true);
            }
            finally
            {
                LoginButton.IsEnabled = true;
                LoginButton.Content = "Connect";
            }
        }

        private async Task<bool> TestConnection()
        {
            try
            {
                switch (currentModel)
                {
                    case "claude":
                    case "claude-sonnet-4-20250514":
                        return await TestClaudeConnection();
                    case "gpt4":
                    case "gpt35":
                        currentModel = "gpt-4o";
                        return await TestOpenAIConnection();
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TestClaudeConnection()
        {
            try
            {
                var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
                {
                    Endpoint = new Uri("https://api.anthropic.com/v1")
                });

                ChatClient chatClient = client.GetChatClient("claude-sonnet-4-20250514");

                var messages = new List<UserChatMessage>
                {
                    new UserChatMessage("Say 'hi'")
                };

                ChatCompletion completion = await chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 1
                });

                return completion != null && completion.Content.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Claude connection test failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestOpenAIConnection()
        {
            var client = new OpenAIClient(new ApiKeyCredential(apiKey));
            try
            {
                var model = client.GetOpenAIModelClient();
                var list = await model.GetModelAsync("gpt-4o");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusText.Text = message;
            StatusText.Visibility = Visibility.Visible;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                StatusText.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }

        private void AddWelcomeMessage()
        {
            var modelName = ModelSelector.SelectedItem as ComboBoxItem;
            var welcomeText = $"Connected to {modelName?.Content}! I'm ready to help you with your code. You can:\n\n" +
                            "• Ask questions about your code\n" +
                            "• Request explanations or improvements\n" +
                            "• Get help with debugging\n" +
                            "• Use the quick action buttons below";

            AddMessage("assistant", welcomeText);
        }

        private void AddMessage(string role, string content)
        {
          var messagePanel = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Background = role == "user" ? new SolidColorBrush(Colors.Gray) : null,
                HorizontalAlignment = role == "user" ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 300,
                Margin = new Thickness(role == "user" ? 50 : 0, 0, role == "user" ? 0 : 50, 0)
            };

            var textBlock = new TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = role == "user" ?
                    (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextOnAccentFillColorSecondaryBrush"] :
                    (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
            };

            messagePanel.Child = textBlock;
            ChatContainer.Children.Add(messagePanel);

            ChatScrollViewer.ChangeView(null, ChatScrollViewer.ScrollableHeight, null);
        }

        private async void SendMessage(string message, bool includeCode = false)
        {
            if (!isConnected || string.IsNullOrWhiteSpace(message)) return;

            string fullMessage = message;

            if (includeCode && parentWindow != null)
            {
                string selectedCode = parentWindow.GetSelectedCode();
                if (!string.IsNullOrEmpty(selectedCode))
                {
                    fullMessage = $"{message}\n\n```\n{selectedCode}\n```";
                }
            }

            AddMessage("user", message);
            MessageInput.Text = "";

            ShowTypingIndicator(true);

            try
            {
                string response = await GetAIResponse(fullMessage);
                AddMessage("assistant", response);
            }
            catch (Exception ex)
            {
                AddMessage("assistant", $"Error: {ex.Message}");
            }
            finally
            {
                ShowTypingIndicator(false);
            }
        }

        private async Task<string> GetAIResponse(string message)
        {
            if (currentModel == "claude-sonnet-4-20250514")
            {
                var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
                {
                    Endpoint = new Uri("https://api.anthropic.com/v1")
                });

                ChatClient chatClient = client.GetChatClient(currentModel);
                ChatCompletion completion = chatClient.CompleteChat(message);
                return completion.Content[0].Text;
            }
            else
            {
                ChatClient chatClient = new(model: currentModel, credential: new ApiKeyCredential(apiKey));
                ChatCompletion completion = chatClient.CompleteChat(message);
                return completion.Content[0].Text;
            }
        }

        private void ShowTypingIndicator(bool show)
        {
            TypingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                ChatScrollViewer.ChangeView(null, ChatScrollViewer.ScrollableHeight, null);
            }
        }

        private void MessageInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            bool isCtrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (e.Key == VirtualKey.Enter && isCtrlPressed && SendButton.IsEnabled)
            {
                e.Handled = true;
                SendButton_Click(null, null);
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageInput.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                SendMessage(message);
            }
        }

        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string action)
            {
                string message = action switch
                {
                    "explain" => "Please explain the selected code or the current file's functionality.",
                    "fix" => "Please review this code and suggest fixes for any issues or bugs you find.",
                    "optimize" => "Please suggest optimizations for this code to improve performance or readability.",
                    _ => "Please help me with this code."
                };

                SendMessage(message, true);
            }
        }

        private void AttachCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (parentWindow != null)
            {
                string selectedCode = parentWindow.GetSelectedCode();
                if (!string.IsNullOrEmpty(selectedCode))
                {
                    MessageInput.Text += $"\n\n```\n{selectedCode}\n```";
                    MessageInput.Focus(FocusState.Keyboard);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public void Disconnect()
        {
            isConnected = false;
            LoginPanel.Visibility = Visibility.Visible;
            WelcomePanel.Visibility = Visibility.Visible;
            ChatScrollViewer.Visibility = Visibility.Collapsed;
            InputPanel.Visibility = Visibility.Collapsed;
            ChatContainer.Children.Clear();
            chatHistory.Clear();
        }

        // 0 openai, 1 claude, eventually 2 gemini
        private async Task TestAPIAsync(int provider)
        {
            if (provider == 0)
            {
                apiKey = OpenAIKey;
                bool connected = await TestOpenAIConnection();
                if (connected)
                {
                    SaveCredentials();
                    isConnected = true;
                }
                else
                {

                }
            }
            else if (provider == 1)
            {
                apiKey = AnthropicKey;
                bool connected = await TestClaudeConnection();
                if (connected)
                {
                    SaveCredentials();
                    isConnected = true;
                }
                else
                {

                }
            }
        }

        private async void OpenAiApiKeyInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            OpenAIKey = OpenAiApiKeyInput.Text;
            await TestAPIAsync(0);   
        }

        private async void ClaudeApiKeyInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            AnthropicKey = ClaudeApiKeyInput.Text;
            await TestAPIAsync(1);
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
    }
}