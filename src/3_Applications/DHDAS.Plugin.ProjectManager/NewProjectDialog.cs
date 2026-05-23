using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DHDAS.Plugin.ProjectManager;

public sealed class NewProjectDialog : Window
{
    private readonly TextBox _nameBox = new() { Width = 320 };
    private readonly TextBox _pathBox = new() { Width = 250, IsReadOnly = true };
    private readonly TextBox _authorBox = new() { Width = 320 };
    private readonly TextBox _descriptionBox = new()
    {
        Width = 320,
        Height = 80,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap
    };
    private readonly TextBlock _errorBlock = new()
    {
        Foreground = Brushes.Firebrick,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 360
    };

    public NewProjectDialog()
    {
        Title = "新建工程";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Padding = new Thickness(20);

        var browseButton = new Button { Content = "浏览..." };
        browseButton.Click += async (_, _) =>
        {
            var picker = new OpenFolderDialog { Title = "选择工程保存目录" };
            var folderPath = await picker.ShowAsync(this);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                _pathBox.Text = folderPath;
                _errorBlock.Text = string.Empty;
            }
        };

        var pathRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        pathRow.Children.Add(_pathBox);
        pathRow.Children.Add(browseButton);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var confirmButton = new Button { Content = "创建", MinWidth = 88 };
        confirmButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                _errorBlock.Text = "请输入工程名称。";
                return;
            }

            if (string.IsNullOrWhiteSpace(_pathBox.Text))
            {
                _errorBlock.Text = "请选择工程保存目录。";
                return;
            }

            Close((
                _nameBox.Text.Trim(),
                _pathBox.Text.Trim(),
                _authorBox.Text?.Trim() ?? string.Empty,
                _descriptionBox.Text?.Trim() ?? string.Empty));
        };

        var cancelButton = new Button { Content = "取消", MinWidth = 88 };
        cancelButton.Click += (_, _) => Close(null);

        buttonRow.Children.Add(confirmButton);
        buttonRow.Children.Add(cancelButton);

        var layout = new StackPanel { Spacing = 10 };
        layout.Children.Add(new TextBlock { Text = "工程名称 *" });
        layout.Children.Add(_nameBox);
        layout.Children.Add(new TextBlock { Text = "保存路径 *" });
        layout.Children.Add(pathRow);
        layout.Children.Add(new TextBlock { Text = "创建人" });
        layout.Children.Add(_authorBox);
        layout.Children.Add(new TextBlock { Text = "工程说明" });
        layout.Children.Add(_descriptionBox);
        layout.Children.Add(_errorBlock);
        layout.Children.Add(buttonRow);

        Content = layout;
    }
}
