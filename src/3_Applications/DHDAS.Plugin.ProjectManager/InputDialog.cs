using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DHDAS.Plugin.ProjectManager;

public sealed class InputDialog : Window
{
    private readonly TextBox _inputBox = new() { Width = 280 };
    private readonly TextBlock _errorBlock = new() { Foreground = Brushes.Firebrick };

    public InputDialog(string title, string prompt)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Padding = new Thickness(20);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var confirmButton = new Button { Content = "确定", MinWidth = 88 };
        confirmButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_inputBox.Text))
            {
                _errorBlock.Text = "请输入内容。";
                return;
            }

            Close(_inputBox.Text.Trim());
        };

        var cancelButton = new Button { Content = "取消", MinWidth = 88 };
        cancelButton.Click += (_, _) => Close(null);

        buttonRow.Children.Add(confirmButton);
        buttonRow.Children.Add(cancelButton);

        var layout = new StackPanel { Spacing = 10 };
        layout.Children.Add(new TextBlock { Text = prompt });
        layout.Children.Add(_inputBox);
        layout.Children.Add(_errorBlock);
        layout.Children.Add(buttonRow);

        Content = layout;
    }
}
