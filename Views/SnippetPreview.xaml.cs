using System;
using System.Globalization;
using System.Windows.Controls;
using ByteStashClient;
using Flow.Launcher.Plugin.ByteStash.ViewModels;

namespace Flow.Launcher.Plugin.ByteStash.Views
{
    /// <summary>
    /// Interaktionslogik für SnippetPreview.xaml
    /// </summary>
    public partial class SnippetPreview : UserControl
    {
        private IPublicAPI _api;

        /// <summary>
        /// Initializes a new instance of the <see cref="SnippetPreview"/> class.
        /// </summary>
        public SnippetPreview()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Sets the snippet data to be displayed in the preview.
        /// </summary>
        /// <param name="snippet">The snippet to display.</param>
        /// <param name="api">The Flow Launcher API for clipboard operations.</param>
        public void SetSnippet(Snippet snippet, IPublicAPI api)
        {
            _api = api;

            if (snippet == null)
            {
                TitleTextBlock.Text = "No snippet selected";
                DescriptionTextBlock.Text = "";
                CategoriesPanel.ItemsSource = null;
                FragmentsPanel.ItemsSource = null;
                UpdatedAtTextBlock.Text = "";
                ShareCountTextBlock.Text = "";
                DataContext = null;
                return;
            }

            // Create ViewModel and set as DataContext
            var viewModel = new SnippetPreviewViewModel(snippet, api);
            DataContext = viewModel;

            // Set basic information
            TitleTextBlock.Text = snippet.Title ?? "Untitled";
            DescriptionTextBlock.Text = snippet.Description ?? "No description";

            // Set categories
            CategoriesPanel.ItemsSource = snippet.Categories ?? [];

            // Set fragments from ViewModel
            FragmentsPanel.ItemsSource = viewModel.Fragments;

            // Format and set metadata with localized timestamp
            UpdatedAtTextBlock.Text = $"{FormatTimestamp(snippet.Updated_at)}";
            ShareCountTextBlock.Text = $"Shares: {snippet.Share_count}";
        }

        /// <summary>
        /// Formats a timestamp string according to the user's locale.
        /// </summary>
        /// <param name="timestamp">The timestamp string to format.</param>
        /// <returns>A formatted timestamp string.</returns>
        private static string FormatTimestamp(string timestamp)
        {
            if (string.IsNullOrEmpty(timestamp))
            {
                return "Unknown";
            }

            // Common ISO 8601 formats
            string[] formats =
            [
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                "yyyy-MM-ddTHH:mm:ssZ",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd"
            ];

            if (DateTime.TryParseExact(timestamp, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime dateTime))
            {
                // Convert to local time and format according to user's locale
                var localTime = dateTime.ToLocalTime();
                return localTime.ToString("g", CultureInfo.CurrentCulture); // Short date and time
            }

            // Fallback: try generic parsing
            if (DateTime.TryParse(timestamp, out dateTime))
            {
                var localTime = dateTime.ToLocalTime();
                return localTime.ToString("g", CultureInfo.CurrentCulture);
            }

            // If all parsing fails, return the original string
            return timestamp;
        }
    }
}
