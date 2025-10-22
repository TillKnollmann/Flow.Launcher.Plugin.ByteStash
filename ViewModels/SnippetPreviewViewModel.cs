using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ByteStashClient;
using Flow.Launcher.Plugin.ByteStash.Helpers;

namespace Flow.Launcher.Plugin.ByteStash.ViewModels
{
    /// <summary>
    /// ViewModel for the snippet preview.
    /// </summary>
    public class SnippetPreviewViewModel
    {
        /// <summary>
        /// Gets the list of fragment view models.
        /// </summary>
        public List<FragmentViewModel> Fragments { get; }

        /// <summary>
        /// Gets the path to the updated icon.
        /// </summary>
        public string UpdatedIconPath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SnippetPreviewViewModel"/> class.
        /// </summary>
        /// <param name="snippet">The snippet to display.</param>
        /// <param name="api">The Flow Launcher API.</param>
        public SnippetPreviewViewModel(Snippet snippet, IPublicAPI api)
        {
            // Create fragment view models
            Fragments = snippet.Fragments?
                .Select(f => new FragmentViewModel(f, api))
                .ToList() ?? [];

            // Set icon path
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            UpdatedIconPath = Path.Combine(assemblyPath, "Icons", Icon.CLOCK);
        }
    }
}