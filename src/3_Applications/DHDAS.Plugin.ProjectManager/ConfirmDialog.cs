using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DHDAS.Plugin.ProjectManager;

public sealed class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmText = "确定", string cancelText = "取消")
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

        var confirmButton = new Button { Content = confirmText, MinWidth = 88 };
        confirmButton.Click += (_, _) => Close(true);

        var cancelButton = new Button { Content = cancelText, MinWidth = 88 };
        cancelButton.Click += (_, _) => Close(false);

        buttonRow.Children.Add(confirmButton);
        buttonRow.Children.Add(cancelButton);

        var layout = new StackPanel { Spacing = 12 };
        layout.Children.Add(new TextBlock
        {
            Text = message,
            MaxWidth = 360,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        layout.Children.Add(buttonRow);

        Content = layout;
    }
}
