using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DHDAS.Plugin.ProjectManager;

public sealed class MessageDialog : Window
{
    public MessageDialog(string title, string message)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Padding = new Thickness(20);

        var okButton = new Button
        {
            Content = "知道了",
            MinWidth = 88,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        okButton.Click += (_, _) => Close();

        var layout = new StackPanel { Spacing = 12 };
        layout.Children.Add(new TextBlock
        {
            Text = message,
            MaxWidth = 380,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        layout.Children.Add(okButton);

        Content = layout;
    }
}
