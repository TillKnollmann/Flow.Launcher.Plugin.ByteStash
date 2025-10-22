using Flow.Launcher.Plugin.ByteStash.Helpers;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Flow.Launcher.Plugin.ByteStash.ViewModels
{
    /// <summary>
    /// ViewModel for the ByteStash settings view.
    /// </summary>
    public class ByteStashSettingsViewModel : INotifyPropertyChanged
    {
        private readonly PluginInitContext _context;
        private readonly Settings _settings;

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteStashSettingsViewModel"/> class.
        /// </summary>
        /// <param name="context">The plugin init context.</param>
        /// <param name="settings">The plugin settings.</param>
        internal ByteStashSettingsViewModel(PluginInitContext context, Settings settings)
        {
            _context = context;
            _settings = settings;

            // Set the copy icon path once
            if (string.IsNullOrEmpty(OpenUrlPath))
            {
                var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                OpenUrlPath = Path.Combine(assemblyPath, "Icons", Icon.EXTERNAL_LINK);
            }

            // Initialize the OpenUrlCommand
            OpenUrlCommand = new RelayCommand(OpenUrl, CanOpenUrl);
        }

        /// <summary>
        /// Gets or sets the base URL used by the ByteStash plugin; changing this value saves the plugin settings.
        /// </summary>
        public string BaseUrl
        {
            get => _settings.BaseUrl;
            set
            {
                if (_settings.BaseUrl != value)
                {
                    _settings.BaseUrl = value;
                    _context.API.SavePluginSettings();
                    OnPropertyChanged();
                    (OpenUrlCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the API key used by the ByteStash plugin; changing this value saves the plugin settings.
        /// </summary>
        public string ApiKey
        {
            get => _settings.ApiKey;
            set
            {
                if (_settings.ApiKey != value)
                {
                    _settings.ApiKey = value;
                    _context.API.SavePluginSettings();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to search in snippet code content.
        /// </summary>
        public bool SearchInCode
        {
            get => _settings.SearchInCode;
            set
            {
                if (_settings.SearchInCode != value)
                {
                    _settings.SearchInCode = value;
                    _context.API.SavePluginSettings();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the path to the copy icon.
        /// </summary>
        public string OpenUrlPath { get; }

        /// <summary>
        /// Gets the command to open the ByteStash URL in the default browser.
        /// </summary>
        public ICommand OpenUrlCommand { get; }

        /// <summary>
        /// Opens the ByteStash URL in the default browser.
        /// </summary>
        private void OpenUrl()
        {
            if (!string.IsNullOrWhiteSpace(BaseUrl))
            {
                try
                {
                    var url = BaseUrl.Trim();
                    // Ensure the URL has a protocol
                    if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        url = "https://" + url;
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    _context.API.LogException("ByteStash", "Failed to open URL in browser", ex);
                }
            }
        }

        /// <summary>
        /// Determines whether the URL can be opened.
        /// </summary>
        /// <returns>True if the BaseUrl is not empty; otherwise, false.</returns>
        private bool CanOpenUrl()
        {
            return !string.IsNullOrWhiteSpace(BaseUrl);
        }
    }
}