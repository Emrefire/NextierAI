using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using NexTierAI.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Text;

namespace NexTierAI.UI;

public sealed partial class MainWindow : Window
{
    private readonly MentorOrchestrator _orchestrator;
    private readonly ChatHistoryService _dbService;

    private Color _currentAccentColor = Color.FromArgb(255, 59, 130, 246);
    private string _currentTopicMode = "Genel AI";
    private bool _isAutoChangingMode = false;
    private bool _isGenerating = false;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isFirstMessageSent = false;

    private int? _currentSessionId = null;
    private bool _isInsideCodeBlock = false;
    private TextBlock _lastCodeTextBlock;
    private string _incompleteChunk = "";

    // YENİ: Sidebar'ın açık/kapalı durumunu takip eder
    private bool _isSidebarOpen = true;

    public MainWindow()
    {
        this.InitializeComponent();
        _orchestrator = (App.Current as App).Services.GetService<MentorOrchestrator>();
        _dbService = new ChatHistoryService();

        IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        if (appWindow != null) appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 900));

        LogoColorAnim.Begin();

        LoadSessionsSidebar();
        LoadUploadedFiles();

        _ = PlayWelcomeAnimation();
    }

    // GÜNCELLENDİ: Özel kaydırma animasyonlu (Smooth Slide) Menü Aç/Kapat
    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _isSidebarOpen = !_isSidebarOpen;

        var sb = new Storyboard();
        var anim = new DoubleAnimation
        {
            To = _isSidebarOpen ? 300 : 0,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(anim, MainSplitView);
        Storyboard.SetTargetProperty(anim, "OpenPaneLength");

        sb.Children.Add(anim);
        sb.Begin();
    }

    private void NewChat_Click(object sender, RoutedEventArgs e)
    {
        if (_isGenerating) return;
        _currentSessionId = null;
        _isFirstMessageSent = false;

        ChatPanel.Children.Clear();
        WelcomeScreen.Visibility = Visibility.Visible;
        ChatScrollViewer.Visibility = Visibility.Collapsed;
        SessionsListView.SelectedItem = null;
        _ = PlayWelcomeAnimation();
    }

    private void LoadSessionsSidebar(string search = "")
    {
        SessionsListView.ItemsSource = _dbService.GetSessions(search);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        LoadSessionsSidebar(SearchBox.Text);
    }

    private void SessionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isGenerating || SessionsListView.SelectedItem == null) return;

        var selectedSession = (ChatSession)SessionsListView.SelectedItem;
        _currentSessionId = selectedSession.Id;

        ChatPanel.Children.Clear();
        TransitionToChatLayout();

        var history = _dbService.GetHistory(_currentSessionId.Value);
        foreach (var msg in history)
        {
            _isInsideCodeBlock = false;
            _lastCodeTextBlock = null;
            _incompleteChunk = "";
            AddMessageToChat(msg.Sender, msg.Text);
        }

        ChatScrollViewer.UpdateLayout();
        ChatScrollViewer.ChangeView(null, ChatScrollViewer.ScrollableHeight, null);
    }

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (_isGenerating) return;

        if (sender is Button btn && btn.Tag is int sessionId)
        {
            _dbService.DeleteSession(sessionId);

            if (_currentSessionId == sessionId)
            {
                _currentSessionId = null;
                ChatPanel.Children.Clear();

                WelcomeScreen.Visibility = Visibility.Visible;
                ChatScrollViewer.Visibility = Visibility.Collapsed;
                _isFirstMessageSent = false;
                _ = PlayWelcomeAnimation();
            }

            LoadSessionsSidebar(SearchBox.Text);
        }
    }

    private void LoadUploadedFiles()
    {
        FilesListView.ItemsSource = _dbService.GetUploadedFiles();
    }

    private async Task PlayWelcomeAnimation()
    {
        string welcomeMessage = "Merhaba Emre, \nBugün neler inşa ediyoruz?";
        WelcomeText.Text = "";
        foreach (char c in welcomeMessage)
        {
            if (_isFirstMessageSent) return;
            WelcomeText.Text += c;
            await Task.Delay(35);
        }
    }

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isGenerating) return;
        if (string.IsNullOrWhiteSpace(InputTextBox.Text)) SendButton.Opacity = 0.5;
        else SendButton.Opacity = 1.0;
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGenerating) { _cancellationTokenSource?.Cancel(); return; }
        await ProcessMessage();
    }

    private void InputTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
            if (!shiftState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            {
                e.Handled = true;
                if (!_isGenerating && !string.IsNullOrWhiteSpace(InputTextBox.Text)) { _ = ProcessMessage(); }
            }
        }
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".md");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            UploadButton.IsEnabled = false;
            if (!_isFirstMessageSent) TransitionToChatLayout();
            AddMessageToChat("SYSTEM", $"'{file.Name}' okunuyor canım. Bekle...");
            try
            {
                string text = await Windows.Storage.FileIO.ReadTextAsync(file);
                await _orchestrator.IngestKnowledgeAsync(text, "Genel", "Belge", 1, file.Name);
                AddMessageToChat("SYSTEM", $"Harika! '{file.Name}' başarıyla eklendi.");
                _dbService.SaveFileRecord(file.Name);
                LoadUploadedFiles();
            }
            catch (Exception ex) { AddMessageToChat("SYSTEM", $"Hata knk: {ex.Message}"); }
            finally { UploadButton.IsEnabled = true; }
        }
    }

    private async void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isAutoChangingMode || ModeComboBox.SelectedItem == null) return;
        if (ModeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
        {
            string selectedMode = selectedItem.Content.ToString();
            if (selectedMode == _currentTopicMode) return;
            await ShiftSystemMode(selectedMode, false);
        }
    }

    private void TransitionToChatLayout()
    {
        _isFirstMessageSent = true;
        WelcomeScreen.Visibility = Visibility.Collapsed;
        ChatScrollViewer.Visibility = Visibility.Visible;
    }

    private async Task ShiftSystemMode(string newMode, bool isAutoDetected)
    {
        _currentTopicMode = newMode;
        Color targetColor = Color.FromArgb(255, 59, 130, 246);
        switch (newMode)
        {
            case "Web & Frontend": targetColor = Color.FromArgb(255, 6, 182, 212); break;
            case "Veritabanı & SQL": targetColor = Color.FromArgb(255, 168, 85, 247); break;
            case "Backend & API": targetColor = Color.FromArgb(255, 249, 115, 22); break;
            case "DevOps & Cloud": targetColor = Color.FromArgb(255, 34, 197, 94); break;
            case "Yapay Zeka (AI)": targetColor = Color.FromArgb(255, 236, 72, 153); break;
            case "Genel AI": targetColor = Color.FromArgb(255, 59, 130, 246); break;
        }
        if (isAutoDetected)
        {
            _isAutoChangingMode = true;
            foreach (ComboBoxItem item in ModeComboBox.Items)
            {
                if (item.Content != null && item.Content.ToString() == newMode) { ModeComboBox.SelectedItem = item; break; }
            }
            _isAutoChangingMode = false;
        }
        await ChangeTheme(targetColor);
        AddMessageToChat("SYSTEM", $"{newMode} moduna geçildi canım.");
    }

    private async Task ChangeTheme(Color newColor)
    {
        if (_currentAccentColor == newColor) return;
        _currentAccentColor = newColor;
        var sb = new Storyboard();
        var anim = new ColorAnimation { To = newColor, Duration = TimeSpan.FromMilliseconds(500) };
        Storyboard.SetTarget(anim, (SolidColorBrush)RootGrid.Resources["CyberAccentBrush"]);
        Storyboard.SetTargetProperty(anim, "Color");
        sb.Children.Add(anim);
        sb.Begin();
        await Task.Delay(100);
    }

    private StackPanel AddMessageToChat(string sender, string message = "")
    {
        bool isUser = sender == "USER";
        var accentBrush = (SolidColorBrush)RootGrid.Resources["CyberAccentBrush"];
        var cardBgBrush = (SolidColorBrush)RootGrid.Resources["CardBgBrush"];

        var card = new Border
        {
            Background = isUser ? new SolidColorBrush(Color.FromArgb(255, 30, 58, 138)) : cardBgBrush,
            BorderBrush = isUser ? accentBrush : new SolidColorBrush(Color.FromArgb(255, 38, 38, 38)),
            BorderThickness = new Thickness(isUser ? 3 : 1, 0, 0, 0),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(25, 20, 25, 20),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 850,
            Opacity = 0,
            RenderTransform = new CompositeTransform { TranslateY = 15 }
        };

        var mainStack = new StackPanel();
        mainStack.Children.Add(new TextBlock
        {
            Text = sender,
            FontSize = 12,
            FontWeight = FontWeights.Black,
            Foreground = accentBrush,
            Margin = new Thickness(0, 0, 0, 8),
            CharacterSpacing = 50
        });

        var contentContainer = new StackPanel { Spacing = 5 };
        mainStack.Children.Add(contentContainer);
        card.Child = mainStack;
        ChatPanel.Children.Add(card);

        var sb = new Storyboard();
        var fade = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(300) };
        var slide = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(fade, card);
        Storyboard.SetTargetProperty(fade, "Opacity");
        Storyboard.SetTarget(slide, card.RenderTransform);
        Storyboard.SetTargetProperty(slide, "TranslateY");
        sb.Children.Add(fade); sb.Children.Add(slide);
        sb.Begin();

        if (!string.IsNullOrEmpty(message))
        {
            RenderMessageIncremental(contentContainer, message);
        }

        ChatScrollViewer.UpdateLayout();
        ChatScrollViewer.ChangeView(null, ChatScrollViewer.ScrollableHeight, null);
        return contentContainer;
    }

    private void RenderMessageIncremental(StackPanel container, string newChunk)
    {
        newChunk = _incompleteChunk + newChunk;
        _incompleteChunk = "";

        var parts = new List<string>();
        int currentIndex = 0;
        int nextBacktick = newChunk.IndexOf("```", currentIndex);

        while (nextBacktick >= 0)
        {
            parts.Add(newChunk.Substring(currentIndex, nextBacktick - currentIndex));
            parts.Add("```");
            currentIndex = nextBacktick + 3;
            nextBacktick = newChunk.IndexOf("```", currentIndex);
        }

        string lastPart = newChunk.Substring(currentIndex);
        if (lastPart == "`" || lastPart == "``") { _incompleteChunk = lastPart; }
        else if (!string.IsNullOrEmpty(lastPart)) { parts.Add(lastPart); }

        foreach (var part in parts)
        {
            if (part == "```")
            {
                _isInsideCodeBlock = !_isInsideCodeBlock;
                if (!_isInsideCodeBlock) _lastCodeTextBlock = null;
            }
            else
            {
                if (_isInsideCodeBlock) AppendCodePart(container, part);
                else AppendNormalPart(container, part);
            }
        }
    }

    private void AppendCodePart(StackPanel container, string part)
    {
        if (_lastCodeTextBlock == null)
        {
            var codeBlockUI = CreateCodeBlockUIIncremental("", out _lastCodeTextBlock);
            container.Children.Add(codeBlockUI);
        }
        _lastCodeTextBlock.Text += part;
    }

    private void AppendNormalPart(StackPanel container, string part)
    {
        var lastNormalTextBlock = container.Children.LastOrDefault() as TextBlock;
        if (lastNormalTextBlock == null)
        {
            lastNormalTextBlock = new TextBlock
            {
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 229, 229, 229)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24
            };
            container.Children.Add(lastNormalTextBlock);
        }

        var inlineCodeParts = part.Split(new[] { "`" }, StringSplitOptions.None);
        for (int i = 0; i < inlineCodeParts.Length; i++)
        {
            var boldParts = inlineCodeParts[i].Split(new[] { "**" }, StringSplitOptions.None);
            for (int j = 0; j < boldParts.Length; j++)
            {
                if (i % 2 == 1)
                {
                    lastNormalTextBlock.Inlines.Add(new Run { Text = boldParts[j], FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 248, 113, 113)) });
                }
                else if (j % 2 == 1)
                {
                    lastNormalTextBlock.Inlines.Add(new Run { Text = boldParts[j], FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) });
                }
                else
                {
                    lastNormalTextBlock.Inlines.Add(new Run { Text = boldParts[j] });
                }
            }
        }
    }

    private UIElement CreateCodeBlockUIIncremental(string language, out TextBlock codeTextBlock)
    {
        var border = new Border { Background = new SolidColorBrush(Color.FromArgb(255, 13, 17, 23)), CornerRadius = new CornerRadius(10), BorderBrush = new SolidColorBrush(Color.FromArgb(255, 48, 54, 61)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 15, 0, 15) };
        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var headerBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(255, 22, 27, 34)), CornerRadius = new CornerRadius(10, 10, 0, 0), Padding = new Thickness(15, 8, 15, 8), BorderBrush = new SolidColorBrush(Color.FromArgb(255, 48, 54, 61)), BorderThickness = new Thickness(0, 0, 0, 1) };
        var headerGrid = new Grid(); headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var langText = new TextBlock { Text = string.IsNullOrEmpty(language) ? "CODE" : language.ToUpper(), Foreground = new SolidColorBrush(Color.FromArgb(255, 139, 148, 158)), FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(langText, 0); headerGrid.Children.Add(langText);
        var copyButton = new Button { Content = "📋 Kopyala", FontSize = 12, Background = new SolidColorBrush(Colors.Transparent), Foreground = new SolidColorBrush(Color.FromArgb(255, 201, 209, 217)), BorderThickness = new Thickness(0) };

        codeTextBlock = new TextBlock { Text = "", FontFamily = new FontFamily("Consolas"), FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(255, 229, 229, 229)), IsTextSelectionEnabled = true };
        var currentCodeTextBlock = codeTextBlock;

        copyButton.Click += async (s, e) => { var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage(); dataPackage.SetText(currentCodeTextBlock.Text.TrimEnd()); Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage); copyButton.Content = "✔️ Kopyalandı"; copyButton.Foreground = new SolidColorBrush(Color.FromArgb(255, 34, 197, 94)); await Task.Delay(2000); copyButton.Content = "📋 Kopyala"; copyButton.Foreground = new SolidColorBrush(Color.FromArgb(255, 201, 209, 217)); };

        Grid.SetColumn(copyButton, 1); headerGrid.Children.Add(copyButton); headerBorder.Child = headerGrid; Grid.SetRow(headerBorder, 0); mainGrid.Children.Add(headerBorder);
        var scrollViewer = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(15) };
        scrollViewer.Content = codeTextBlock; Grid.SetRow(scrollViewer, 1); mainGrid.Children.Add(scrollViewer); border.Child = mainGrid; return border;
    }

    private async Task ProcessMessage()
    {
        var text = InputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        _isGenerating = true;
        SendButton.Content = "🛑 DURDUR";
        SendButton.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 239, 68, 68));
        SendButton.Opacity = 1.0;
        _cancellationTokenSource = new CancellationTokenSource();
        InputTextBox.Text = string.Empty;

        try
        {
            if (!_isFirstMessageSent) TransitionToChatLayout();

            string lowerText = text.ToLower();
            string targetMode = _currentTopicMode;
            if (lowerText.Contains("react") || lowerText.Contains("frontend") || lowerText.Contains("web") || lowerText.Contains("html")) targetMode = "Web & Frontend";
            else if (lowerText.Contains("sql") || lowerText.Contains("data") || lowerText.Contains("veritabanı") || lowerText.Contains("postgres")) targetMode = "Veritabanı & SQL";
            else if (lowerText.Contains("c#") || lowerText.Contains(".net") || lowerText.Contains("backend") || lowerText.Contains("api")) targetMode = "Backend & API";
            else if (lowerText.Contains("docker") || lowerText.Contains("devops") || lowerText.Contains("sunucu")) targetMode = "DevOps & Cloud";
            else if (lowerText.Contains("yapay zeka") || lowerText.Contains("ai") || lowerText.Contains("llama")) targetMode = "Yapay Zeka (AI)";

            if (targetMode != _currentTopicMode) await ShiftSystemMode(targetMode, true);

            if (_currentSessionId == null)
            {
                string title = text.Length > 25 ? text.Substring(0, 25) + "..." : text;
                _currentSessionId = _dbService.CreateSession(title);
                LoadSessionsSidebar();
            }

            AddMessageToChat("USER", text);
            _dbService.SaveMessage(_currentSessionId.Value, "USER", text);

            var contentContainer = AddMessageToChat("NEXTIER", "");
            var statusText = new TextBlock { Text = "Sistem taranıyor...", Foreground = new SolidColorBrush(Color.FromArgb(255, 156, 163, 175)), FontStyle = FontStyle.Italic, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
            var ring = new ProgressRing { IsActive = true, Width = 18, Height = 18, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = (SolidColorBrush)RootGrid.Resources["CyberAccentBrush"] };
            var thinkingStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            thinkingStack.Children.Add(ring); thinkingStack.Children.Add(statusText);
            contentContainer.Children.Add(thinkingStack);

            _isInsideCodeBlock = false;
            _lastCodeTextBlock = null;
            _incompleteChunk = "";

            string fullResponse = "";

            _ = Task.Run(async () => {
                string[] statuses = { "Hafıza taranıyor...", "Vektörler analiz ediliyor...", "NexTier Core düşünüyor...", "Bilgi sentezleniyor...", "Yanıt hazırlanıyor..." };
                int i = 0;
                while (_isGenerating)
                {
                    await Task.Delay(2000);
                    if (!_isGenerating) break;
                    this.DispatcherQueue.TryEnqueue(() => { statusText.Text = statuses[i % statuses.Length]; });
                    i++;
                }
            });

            bool isFirstChunk = true;

            await foreach (var chunk in _orchestrator.AskQuestionStreamAsync(text, _cancellationTokenSource.Token))
            {
                if (isFirstChunk)
                {
                    contentContainer.Children.Clear();
                    isFirstChunk = false;
                }

                fullResponse += chunk;
                RenderMessageIncremental(contentContainer, chunk);
                await Task.Delay(15);

                if (ChatScrollViewer.ScrollableHeight - ChatScrollViewer.VerticalOffset < 50)
                {
                    ChatScrollViewer.UpdateLayout();
                    ChatScrollViewer.ChangeView(null, ChatScrollViewer.ScrollableHeight, null);
                }
            }

            _dbService.SaveMessage(_currentSessionId.Value, "NEXTIER", fullResponse);
        }
        catch (Exception ex)
        {
            AddMessageToChat("SYSTEM", $"Kritik bir hata oluştu: {ex.Message}");
        }
        finally
        {
            _isGenerating = false;
            SendButton.Content = "🚀 GÖNDER";
            SendButton.Background = (SolidColorBrush)RootGrid.Resources["CyberAccentBrush"];
            SendButton.Opacity = 0.5;
        }
    }
}