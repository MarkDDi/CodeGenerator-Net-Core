using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CodeGenerator.Events;
using Prism.Events;

namespace CodeGenerator.Views;

public partial class MainWindow : Window
{
    private readonly IEventAggregator _eventAggregator;

    public MainWindow(IEventAggregator eventAggregator)
    {
        InitializeComponent();

        _eventAggregator = eventAggregator;
    }

    private void MenuItem_DeleteButtonOnClick(object sender, RoutedEventArgs e)
    {
        _eventAggregator.GetEvent<DirectoryEvent>().Publish(DirListBox.SelectedIndex);
    }

    private void DeleteFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        _eventAggregator.GetEvent<FileNameTagEvent>().Publish(button.Tag.ToString());
    }

    private void DeleteFileSuffixButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        _eventAggregator.GetEvent<FileSuffixTagEvent>().Publish(button.Tag.ToString());
    }
}