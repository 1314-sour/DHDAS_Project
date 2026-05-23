using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DHDAS.Plugin.ProjectManager;

public sealed class EditProjectDialog : Window
{
    private readonly TextBox _nameBox;
    private readonly TextBox _descriptionBox;
    private readonly TextBlock _errorBlock = new() { Foreground = Brushes.Firebrick };

    public EditProjectDialog(string currentName, string currentDescription)
    {
        Title = "编辑工程";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Padding = new Thickness(20);

        _nameBox = new TextBox { Width = 320, Text = currentName };
        _descriptionBox = new TextBox
        {
            Width = 320,
            Height = 90,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Text = currentDescription
        };

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var saveButton = new Button { Content = "保存", MinWidth = 88 };
        saveButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                _errorBlock.Text = "工程名称不能为空。";
                return;
            }

            Close((_nameBox.Text.Trim(), _descriptionBox.Text?.Trim() ?? string.Empty));
        };

        var cancelButton = new Button { Content = "取消", MinWidth = 88 };
        cancelButton.Click += (_, _) => Close(null);

        buttonRow.Children.Add(saveButton);
        buttonRow.Children.Add(cancelButton);

        var layout = new StackPanel { Spacing = 10 };
        layout.Children.Add(new TextBlock { Text = "工程名称 *" });
        layout.Children.Add(_nameBox);
        layout.Children.Add(new TextBlock { Text = "工程说明" });
        layout.Children.Add(_descriptionBox);
        layout.Children.Add(_errorBlock);
        layout.Children.Add(buttonRow);

        Content = layout;
    }
}
