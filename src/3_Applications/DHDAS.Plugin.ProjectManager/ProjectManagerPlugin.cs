using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DHDAS.Application.Support;
using DHDAS.Plugin.ProjectManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DHDAS.Plugin.ProjectManager;

public sealed class ProjectManagerPlugin : PluginBase
{
    private const double MainStackBreakpoint = 980;
    private const double DetailStackBreakpoint = 760;
    private const double ActionStackBreakpoint = 620;
    private const double ToolbarStackBreakpoint = 720;
    private const string EditProjectShortText = "\u7f16\u8f91";
    private const string DeleteProjectShortText = "\u5220\u9664";
    private const string EditProjectFailedTitle = "\u7f16\u8f91\u5de5\u7a0b\u5931\u8d25";
    private const string DeleteProjectDialogTitle = "\u5220\u9664\u5de5\u7a0b";
    private const string DeleteProjectItemTypeText = "\u5de5\u7a0b";
    private const string DeleteProjectFilesText = "\u540c\u65f6\u5220\u9664\u672c\u5730\u5de5\u7a0b\u6587\u4ef6";
    private const string DeleteProjectFailedTitle = "\u5220\u9664\u5de5\u7a0b\u5931\u8d25";

    private Control? _cachedView;
    private ProjectManagerViewModel? _viewModel;

    public override string PluginId => "PROJECT_MANAGER";
    public override string DisplayName => "工程管理";
    public override int Priority => 20;

    public override Control CreateView(IServiceProvider serviceProvider)
    {
        if (_cachedView != null)
        {
            return _cachedView;
        }

        _viewModel = ActivatorUtilities.CreateInstance<ProjectManagerViewModel>(serviceProvider);
        _ = _viewModel.InitializeAsync();

        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("300,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(12)
        };

        var leftPanel = BuildRecentProjectsPanel(
            root,
            _viewModel,
            out var recentProjectList,
            out var primaryButtons,
            out var secondaryButtons);
        Grid.SetColumn(leftPanel, 0);
        Grid.SetRow(leftPanel, 0);
        root.Children.Add(leftPanel);

        var rightPanel = BuildWorkspacePanel(
            root,
            _viewModel,
            out var toolbar,
            out var experimentContent,
            out var experimentList,
            out var detailPanel);
        Grid.SetColumn(rightPanel, 1);
        Grid.SetRow(rightPanel, 0);
        root.Children.Add(rightPanel);

        var statusPanel = BuildStatusPanel(_viewModel);
        Grid.SetColumn(statusPanel, 0);
        Grid.SetColumnSpan(statusPanel, 2);
        Grid.SetRow(statusPanel, 1);
        root.Children.Add(statusPanel);

        var responsiveLayout = new ResponsiveLayoutContext
        {
            Root = root,
            LeftPanel = leftPanel,
            RightPanel = rightPanel,
            StatusPanel = statusPanel,
            RecentProjectList = recentProjectList,
            PrimaryButtons = primaryButtons,
            SecondaryButtons = secondaryButtons,
            Toolbar = toolbar,
            ExperimentContent = experimentContent,
            ExperimentList = experimentList,
            DetailPanel = detailPanel
        };

        root.AttachedToVisualTree += (_, _) => ApplyResponsiveLayout(responsiveLayout, root.Bounds.Width);
        root.PropertyChanged += (_, args) =>
        {
            if (args.Property == Visual.BoundsProperty)
            {
                ApplyResponsiveLayout(responsiveLayout, root.Bounds.Width);
            }
        };

        root.DataContext = _viewModel;

        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = root
        };

        _cachedView = scrollViewer;
        return scrollViewer;
    }

    public override void OnUnloaded()
    {
        _viewModel?.OnDeactivated();
        _viewModel = null;
        _cachedView = null;
    }

    private static Control BuildRecentProjectsPanel(
        Control root,
        ProjectManagerViewModel viewModel,
        out ListBox projectList,
        out StackPanel primaryButtons,
        out StackPanel secondaryButtons)
    {
        var panel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto")
        };

        panel.Children.Add(new TextBlock
        {
            Text = "最近工程",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold
        });

        var hint = new TextBlock
        {
            Text = "单击预览，双击打开。",
            Foreground = Brushes.Gray,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };
        Grid.SetRow(hint, 1);
        panel.Children.Add(hint);

        var projectListControl = new ListBox
        {
            MinHeight = 200,
            BorderBrush = Brushes.Gainsboro,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 12, 0, 0)
        };
        projectListControl.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(ProjectManagerViewModel.RecentProjects)));
        projectListControl.Bind(ListBox.SelectedItemProperty, new Binding(nameof(ProjectManagerViewModel.SelectedProject))
        {
            Mode = BindingMode.TwoWay
        });
        projectListControl.ItemTemplate = new FuncDataTemplate<ProjectNodeViewModel>((project, _) =>
        {
            var container = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                Margin = new Thickness(6)
            };

            var infoPanel = new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var titleRow = new DockPanel();
            var nameBlock = new TextBlock
            {
                Text = project?.Name ?? string.Empty,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13
            };
            DockPanel.SetDock(nameBlock, Dock.Left);
            titleRow.Children.Add(nameBlock);

            var badge = new TextBlock
            {
                Text = project?.IsCurrent == true ? "当前" : "最近",
                Foreground = project?.IsCurrent == true ? Brushes.ForestGreen : Brushes.SlateGray,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(badge, Dock.Right);
            titleRow.Children.Add(badge);
            infoPanel.Children.Add(titleRow);

            infoPanel.Children.Add(new TextBlock
            {
                Text = project?.Root ?? string.Empty,
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap
            });

            Grid.SetColumn(infoPanel, 0);
            container.Children.Add(infoPanel);

            var editButton = new Button
            {
                Content = EditProjectShortText,
                MinWidth = 56,
                Padding = new Thickness(10, 4),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            editButton.Click += async (_, args) =>
            {
                args.Handled = true;
                if (project == null)
                {
                    return;
                }

                projectListControl.SelectedItem = project;
                await ShowEditProjectDialogAsync(root, viewModel, project);
            };
            editButton.AddHandler(InputElement.DoubleTappedEvent, (_, args) => args.Handled = true);
            Grid.SetColumn(editButton, 1);
            container.Children.Add(editButton);

            var deleteButton = new Button
            {
                Content = DeleteProjectShortText,
                MinWidth = 56,
                Padding = new Thickness(10, 4),
                Foreground = Brushes.Firebrick,
                VerticalAlignment = VerticalAlignment.Center
            };
            deleteButton.Click += async (_, args) =>
            {
                args.Handled = true;
                if (project == null)
                {
                    return;
                }

                projectListControl.SelectedItem = project;
                await ShowDeleteProjectDialogAsync(root, viewModel, project);
            };
            deleteButton.AddHandler(InputElement.DoubleTappedEvent, (_, args) => args.Handled = true);
            Grid.SetColumn(deleteButton, 2);
            container.Children.Add(deleteButton);

            return container;
        });
        projectListControl.SelectionChanged += async (_, _) =>
        {
            if (projectListControl.SelectedItem is ProjectNodeViewModel project)
            {
                await viewModel.PreviewProjectAsync(project);
            }
        };
        projectListControl.AddHandler(InputElement.DoubleTappedEvent, async (_, _) =>
        {
            if (!await viewModel.OpenSelectedProjectAsync())
            {
                await ShowMessageAsync(root, "打开工程失败", viewModel.DetailMessage);
            }
        });
        projectList = projectListControl;
        Grid.SetRow(projectListControl, 2);
        panel.Children.Add(projectListControl);

        primaryButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var newProjectButton = new Button
        {
            Content = "新建工程",
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        newProjectButton.Click += async (_, _) =>
        {
            var dialog = new NewProjectDialog();
            var result = await dialog.ShowDialog<(string Name, string Path, string Author, string Description)?>(RequireOwner(root));
            if (result.HasValue &&
                !await viewModel.CreateProjectAsync(result.Value.Name, result.Value.Path, result.Value.Author, result.Value.Description))
            {
                await ShowMessageAsync(root, "新建工程失败", viewModel.DetailMessage);
            }
        };
        primaryButtons.Children.Add(newProjectButton);

        var openProjectButton = new Button
        {
            Content = "打开工程",
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        openProjectButton.Click += async (_, _) =>
        {
            var picker = new OpenFolderDialog { Title = "选择工程目录" };
            var selectedPath = await picker.ShowAsync(RequireOwner(root));
            if (!string.IsNullOrWhiteSpace(selectedPath) && !await viewModel.OpenProjectAsync(selectedPath))
            {
                await ShowMessageAsync(root, "打开工程失败", viewModel.DetailMessage);
            }
        };
        primaryButtons.Children.Add(openProjectButton);
        Grid.SetRow(primaryButtons, 3);
        panel.Children.Add(primaryButtons);

        secondaryButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var editButton = new Button
        {
            Content = "编辑工程",
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        editButton.IsVisible = false;
        editButton.Click += async (_, _) =>
        {
            if (viewModel.SelectedProject == null)
            {
                await ShowMessageAsync(root, "未选择工程", "请先选择一个工程。");
                return;
            }

            var dialog = new EditProjectDialog(
                viewModel.SelectedProject.Name,
                viewModel.SelectedProject.Meta?.Description ?? string.Empty);
            var result = await dialog.ShowDialog<(string Name, string Description)?>(RequireOwner(root));
            if (result.HasValue &&
                !await viewModel.UpdateSelectedProjectAsync(result.Value.Name, result.Value.Description))
            {
                await ShowMessageAsync(root, "编辑工程失败", viewModel.DetailMessage);
            }
        };
        secondaryButtons.Children.Add(editButton);

        var closeButton = new Button
        {
            Content = "关闭工程",
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        closeButton.Click += async (_, _) =>
        {
            if (!await viewModel.CloseCurrentProjectAsync())
            {
                await ShowMessageAsync(root, "关闭工程失败", viewModel.DetailMessage);
            }
        };
        secondaryButtons.Children.Add(closeButton);

        var deleteButton = new Button
        {
            Content = "删除工程",
            Foreground = Brushes.Firebrick,
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        deleteButton.IsVisible = false;
        deleteButton.Click += async (_, _) =>
        {
            if (viewModel.SelectedProject == null)
            {
                await ShowMessageAsync(root, "未选择工程", "请先选择一个工程。");
                return;
            }

            var dialog = new DeleteItemDialog(
                "删除工程",
                "工程",
                viewModel.SelectedProject.Name,
                "同时删除本地工程文件");
            var result = await dialog.ShowDialog<(bool Confirmed, bool DeleteFiles)?>(RequireOwner(root));
            if (result?.Confirmed == true && !await viewModel.DeleteSelectedProjectAsync(result.Value.DeleteFiles))
            {
                await ShowMessageAsync(root, "删除工程失败", viewModel.DetailMessage);
            }
        };
        secondaryButtons.Children.Add(deleteButton);
        Grid.SetRow(secondaryButtons, 4);
        panel.Children.Add(secondaryButtons);

        return panel;
    }

    private static Control BuildWorkspacePanel(
        Control root,
        ProjectManagerViewModel viewModel,
        out StackPanel toolbar,
        out Grid content,
        out ListBox experimentList,
        out Grid detailPanel)
    {
        var panel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto")
        };

        var infoCard = new Border
        {
            BorderBrush = Brushes.Gainsboro,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14)
        };

        var infoStack = new StackPanel { Spacing = 4 };
        var title = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.SemiBold
        };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(ProjectManagerViewModel.ProjectTitle)));
        infoStack.Children.Add(title);

        var state = new TextBlock
        {
            Foreground = Brushes.ForestGreen,
            FontSize = 12
        };
        state.Bind(TextBlock.TextProperty, new Binding(nameof(ProjectManagerViewModel.ProjectStateText)));
        infoStack.Children.Add(state);

        var description = new TextBlock
        {
            Foreground = Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap
        };
        description.Bind(TextBlock.TextProperty, new Binding(nameof(ProjectManagerViewModel.ProjectDescription)));
        infoStack.Children.Add(description);

        var path = new TextBlock
        {
            Foreground = Brushes.Gray,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        path.Bind(TextBlock.TextProperty, new Binding(nameof(ProjectManagerViewModel.ProjectPathText)));
        infoStack.Children.Add(path);

        var experimentState = new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            FontWeight = FontWeight.Medium
        };
        experimentState.Bind(TextBlock.TextProperty, new Binding(nameof(ProjectManagerViewModel.ExperimentStateText)));
        infoStack.Children.Add(experimentState);

        infoCard.Child = infoStack;
        Grid.SetRow(infoCard, 0);
        panel.Children.Add(infoCard);

        toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var newExperimentButton = new Button
        {
            Content = "新建实验",
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        newExperimentButton.Click += async (_, _) =>
        {
            var dialog = new InputDialog("新建实验", "请输入实验名称");
            var experimentName = await dialog.ShowDialog<string?>(RequireOwner(root));
            if (!string.IsNullOrWhiteSpace(experimentName) && !await viewModel.CreateExperimentAsync(experimentName))
            {
                await ShowMessageAsync(root, "新建实验失败", viewModel.DetailMessage);
            }
        };
        toolbar.Children.Add(newExperimentButton);

        var importExperimentButton = new Button
        {
            Content = "导入实验",
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        importExperimentButton.Click += async (_, _) =>
        {
            var picker = new OpenFolderDialog
            {
                Title = "从当前工程中选择实验目录",
                Directory = viewModel.CurrentExperimentsRoot
            };
            var selectedPath = await picker.ShowAsync(RequireOwner(root));
            if (!string.IsNullOrWhiteSpace(selectedPath) && !await viewModel.ImportExperimentAsync(selectedPath))
            {
                await ShowMessageAsync(root, "导入实验失败", viewModel.DetailMessage);
            }
        };
        toolbar.Children.Add(importExperimentButton);

        var deleteExperimentButton = new Button
        {
            Content = "删除实验",
            Foreground = Brushes.Firebrick,
            MinWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        deleteExperimentButton.Click += async (_, _) =>
        {
            if (viewModel.SelectedExperiment == null)
            {
                await ShowMessageAsync(root, "未选择实验", "请先选择一个实验。");
                return;
            }

            var dialog = new DeleteItemDialog(
                "删除实验",
                "实验",
                viewModel.SelectedExperiment.Name,
                "同时删除本地实验文件");
            var result = await dialog.ShowDialog<(bool Confirmed, bool DeleteFiles)?>(RequireOwner(root));
            if (result?.Confirmed == true && !await viewModel.DeleteSelectedExperimentAsync(result.Value.DeleteFiles))
            {
                await ShowMessageAsync(root, "删除实验失败", viewModel.DetailMessage);
            }
        };
        toolbar.Children.Add(deleteExperimentButton);

        var progressBar = new ProgressBar
        {
            Width = 140,
            Height = 6,
            IsIndeterminate = true,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        progressBar.Bind(Visual.IsVisibleProperty, new Binding(nameof(ProjectManagerViewModel.IsBusy)));
        toolbar.Children.Add(progressBar);

        Grid.SetRow(toolbar, 1);
        panel.Children.Add(toolbar);

        content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("280,*"),
            Margin = new Thickness(0, 10, 0, 0)
        };

        var experimentListControl = new ListBox
        {
            MinHeight = 180,
            BorderBrush = Brushes.Gainsboro,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6)
        };
        experimentListControl.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(ProjectManagerViewModel.Experiments)));
        experimentListControl.Bind(ListBox.SelectedItemProperty, new Binding(nameof(ProjectManagerViewModel.SelectedExperiment))
        {
            Mode = BindingMode.TwoWay
        });
        experimentListControl.ItemTemplate = new FuncDataTemplate<ExperimentNodeViewModel>((experiment, _) =>
        {
            var stack = new StackPanel
            {
                Margin = new Thickness(6),
                Spacing = 2
            };
            stack.Children.Add(new TextBlock
            {
                Text = experiment?.Name ?? string.Empty,
                FontWeight = FontWeight.SemiBold
            });
            stack.Children.Add(new TextBlock
            {
                Text = experiment?.CreatedAtText ?? string.Empty,
                Foreground = Brushes.Gray,
                FontSize = 11
            });
            return stack;
        });
        experimentListControl.SelectionChanged += async (_, _) =>
        {
            if (experimentListControl.SelectedItem is ExperimentNodeViewModel experiment)
            {
                await viewModel.PreviewExperimentAsync(experiment);
            }
        };
        experimentList = experimentListControl;
        Grid.SetColumn(experimentListControl, 0);
        content.Children.Add(experimentListControl);

        detailPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        detailPanel.Children.Add(new TextBlock
        {
            Text = "实验详情树",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold
        });

        var detailList = new ListBox
        {
            BorderBrush = Brushes.Gainsboro,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 6, 0, 0)
        };
        detailList.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(ProjectManagerViewModel.ExperimentDetails)));
        detailList.ItemTemplate = new FuncDataTemplate<ExperimentTreeNodeViewModel>((entry, _) =>
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Margin = entry?.Indent ?? default
            };

            row.Children.Add(new TextBlock
            {
                Text = entry?.IsDirectory == true ? "[目录]" : "[文件]",
                Foreground = entry?.IsDirectory == true ? Brushes.SteelBlue : Brushes.Gray,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = entry?.Name ?? string.Empty,
                VerticalAlignment = VerticalAlignment.Center
            });
            return row;
        });
        Grid.SetRow(detailList, 1);
        detailPanel.Children.Add(detailList);
        Grid.SetColumn(detailPanel, 1);
        content.Children.Add(detailPanel);

        Grid.SetRow(content, 2);
        panel.Children.Add(content);

        var footer = new TextBlock
        {
            Foreground = Brushes.Gray,
            FontSize = 12,
            Text = "提示：如果删除实验时保留了本地文件，后续仍可再次导入。",
            Margin = new Thickness(0, 10, 0, 0)
        };
        Grid.SetRow(footer, 3);
        panel.Children.Add(footer);

        return panel;
    }

    private static Control BuildStatusPanel(ProjectManagerViewModel viewModel)
    {
        var panel = new Border
        {
            BorderBrush = Brushes.Gainsboro,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 10, 0, 0)
        };

        var content = new StackPanel { Spacing = 2 };

        var status = new TextBlock
        {
            Foreground = Brushes.DarkSlateBlue,
            FontWeight = FontWeight.Medium
        };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(ProjectManagerViewModel.StatusMessage)));
        content.Children.Add(status);

        var detail = new TextBlock
        {
            Foreground = Brushes.Gray,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        detail.Bind(TextBlock.TextProperty, new Binding(nameof(ProjectManagerViewModel.DetailMessage)));
        content.Children.Add(detail);

        panel.Child = content;
        return panel;
    }

    private static void ApplyResponsiveLayout(ResponsiveLayoutContext layout, double width)
    {
        if (width <= 0)
        {
            return;
        }

        var stackMain = width < MainStackBreakpoint;
        var stackDetail = width < DetailStackBreakpoint;
        var stackActions = width < ActionStackBreakpoint;
        var stackToolbar = width < ToolbarStackBreakpoint;

        layout.Root.ColumnDefinitions = stackMain
            ? new ColumnDefinitions("*")
            : new ColumnDefinitions("300,*");
        layout.Root.RowDefinitions = stackMain
            ? new RowDefinitions("Auto,Auto,Auto")
            : new RowDefinitions("*,Auto");

        Grid.SetColumn(layout.LeftPanel, 0);
        Grid.SetRow(layout.LeftPanel, 0);
        layout.LeftPanel.Margin = default;

        Grid.SetColumn(layout.RightPanel, stackMain ? 0 : 1);
        Grid.SetRow(layout.RightPanel, stackMain ? 1 : 0);
        layout.RightPanel.Margin = stackMain ? new Thickness(0, 12, 0, 0) : new Thickness(12, 0, 0, 0);

        Grid.SetColumn(layout.StatusPanel, 0);
        Grid.SetColumnSpan(layout.StatusPanel, stackMain ? 1 : 2);
        Grid.SetRow(layout.StatusPanel, stackMain ? 2 : 1);
        layout.StatusPanel.Margin = new Thickness(0, 12, 0, 0);

        layout.RecentProjectList.MinHeight = stackMain ? 160 : 200;
        layout.PrimaryButtons.Orientation = stackActions ? Orientation.Vertical : Orientation.Horizontal;
        layout.SecondaryButtons.Orientation = stackActions ? Orientation.Vertical : Orientation.Horizontal;
        layout.Toolbar.Orientation = stackToolbar ? Orientation.Vertical : Orientation.Horizontal;

        layout.ExperimentContent.ColumnDefinitions = stackDetail
            ? new ColumnDefinitions("*")
            : new ColumnDefinitions("280,*");
        layout.ExperimentContent.RowDefinitions = stackDetail
            ? new RowDefinitions("Auto,Auto")
            : new RowDefinitions("*");

        Grid.SetColumn(layout.ExperimentList, 0);
        Grid.SetRow(layout.ExperimentList, 0);

        Grid.SetColumn(layout.DetailPanel, stackDetail ? 0 : 1);
        Grid.SetRow(layout.DetailPanel, stackDetail ? 1 : 0);
        layout.DetailPanel.Margin = stackDetail ? new Thickness(0, 10, 0, 0) : new Thickness(10, 0, 0, 0);
    }

    private static Window RequireOwner(Control root) =>
        TopLevel.GetTopLevel(root) as Window
        ?? throw new InvalidOperationException("未找到所属窗口。");

    private static async Task ShowEditProjectDialogAsync(
        Control root,
        ProjectManagerViewModel viewModel,
        ProjectNodeViewModel project)
    {
        viewModel.SelectedProject = project;
        await viewModel.PreviewProjectAsync(project);

        var dialog = new EditProjectDialog(
            project.Name,
            project.Meta?.Description ?? string.Empty);
        var result = await dialog.ShowDialog<(string Name, string Description)?>(RequireOwner(root));
        if (result.HasValue &&
            !await viewModel.UpdateSelectedProjectAsync(result.Value.Name, result.Value.Description))
        {
            await ShowMessageAsync(root, EditProjectFailedTitle, viewModel.DetailMessage);
        }
    }

    private static async Task ShowDeleteProjectDialogAsync(
        Control root,
        ProjectManagerViewModel viewModel,
        ProjectNodeViewModel project)
    {
        viewModel.SelectedProject = project;
        await viewModel.PreviewProjectAsync(project);

        var dialog = new DeleteItemDialog(
            DeleteProjectDialogTitle,
            DeleteProjectItemTypeText,
            project.Name,
            DeleteProjectFilesText);
        var result = await dialog.ShowDialog<(bool Confirmed, bool DeleteFiles)?>(RequireOwner(root));
        if (result?.Confirmed == true && !await viewModel.DeleteSelectedProjectAsync(result.Value.DeleteFiles))
        {
            await ShowMessageAsync(root, DeleteProjectFailedTitle, viewModel.DetailMessage);
        }
    }

    private static async Task ShowMessageAsync(Control root, string title, string message)
    {
        var dialog = new MessageDialog(title, message);
        await dialog.ShowDialog(RequireOwner(root));
    }

    private sealed class ResponsiveLayoutContext
    {
        public Grid Root { get; init; } = null!;
        public Control LeftPanel { get; init; } = null!;
        public Control RightPanel { get; init; } = null!;
        public Control StatusPanel { get; init; } = null!;
        public ListBox RecentProjectList { get; init; } = null!;
        public StackPanel PrimaryButtons { get; init; } = null!;
        public StackPanel SecondaryButtons { get; init; } = null!;
        public StackPanel Toolbar { get; init; } = null!;
        public Grid ExperimentContent { get; init; } = null!;
        public ListBox ExperimentList { get; init; } = null!;
        public Control DetailPanel { get; init; } = null!;
    }
}
