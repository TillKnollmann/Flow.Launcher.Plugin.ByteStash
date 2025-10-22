using System.Windows.Controls;
using Flow.Launcher.Plugin.ByteStash.ViewModels;

namespace Flow.Launcher.Plugin.ByteStash.Views;

/// <summary>
/// Interaction logic for ByteStashSettings.
/// </summary>
public partial class ByteStashSettings : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ByteStashSettings"/> control.
    /// </summary>
    /// <param name="viewModel">The view model to use as the DataContext.</param>
    public ByteStashSettings(ByteStashSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}