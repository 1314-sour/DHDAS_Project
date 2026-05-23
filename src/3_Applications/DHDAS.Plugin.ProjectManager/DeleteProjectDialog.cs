using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DHDAS.Plugin.ProjectManager;

public sealed class DeleteItemDialog : Window
{
    public DeleteItemDialog(string title, string itemType, string itemName, string checkBoxText)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Padding = new Thickness(20);

        var deleteFilesCheckBox = new CheckBox
        {
            Content = checkBoxText,
            IsChecked = false
        };

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var confirmButton = new Button
        {
            Content = "确认删除",
            MinWidth = 96,
            Foreground = Brushes.Firebrick
        };
        confirmButton.Click += (_, _) => Close((true, deleteFilesCheckBox.IsChecked == true));

        var cancelButton = new Button { Content = "取消", MinWidth = 88 };
        cancelButton.Click += (_, _) => Close(((bool Confirmed, bool DeleteFiles)?)(false, false));

        buttonRow.Children.Add(confirmButton);
        buttonRow.Children.Add(cancelButton);

        var layout = new StackPanel { Spacing = 12 };
        layout.Children.Add(new TextBlock
        {
            Text = $"确定要删除{itemType}“{itemName}”吗？",
            MaxWidth = 360,
            TextWrapping = TextWrapping.Wrap
        });
        layout.Children.Add(deleteFilesCheckBox);
        layout.Children.Add(buttonRow);

        Content = layout;
    }
}
