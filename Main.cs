using Flow.Launcher.Plugin.ByteStash.Helpers;
using Flow.Launcher.Plugin.ByteStash.Resources;
using Flow.Launcher.Plugin.ByteStash.ViewModels;
using Flow.Launcher.Plugin.ByteStash.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.ByteStash
{
    /// <summary>
    /// Represents the ByteStash plugin for Flow Launcher.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class ByteStash : IPlugin, ISettingProvider, IContextMenu
    {
        private static readonly HttpClient _httpClient = new();
        private ByteStashClient.ByteStashClient _byteStashClient;
        private ByteStashSettingsViewModel _viewModel;
        private Settings _settings;
        private PluginInitContext _context;
        private String _iconsPath;

        /// <summary>
        /// Gets or creates the ByteStash client with current settings.
        /// </summary>
        private ByteStashClient.ByteStashClient GetClient()
        {
            _httpClient.DefaultRequestHeaders.Clear();

            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey);
            }


            _byteStashClient ??= new ByteStashClient.ByteStashClient(_httpClient);
            _byteStashClient.BaseUrl = _settings.BaseUrl;

            return _byteStashClient;
        }

        /// <summary>
        /// Creates and returns the settings panel control for the ByteStash plugin.
        /// </summary>
        public Control CreateSettingPanel()
        {
            return new ByteStashSettings(_viewModel);
        }

        /// <summary>
        /// Initializes the plugin with the provided PluginInitContext.
        /// </summary>
        public void Init(PluginInitContext context)
        {
            _context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();
            _viewModel = new ByteStashSettingsViewModel(_context, _settings);
            _iconsPath = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Icons");
        }

        /// <summary>
        /// Handles a search query and returns a list of results.
        /// </summary>
        /// <param name="query">The query entered by the user.</param>
        /// <returns>A list of Result objects matching the query.</returns>
        public List<Result> Query(Query query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_settings.BaseUrl) || string.IsNullOrWhiteSpace(_settings.ApiKey))
                {
                    return StaticResultProvider.GetInvalidSettingsResults(_context, _iconsPath);
                }

                if (query.Search.TrimStart().StartsWith('+'))
                {
                    return HandleCreateSnippet(query.Search.TrimStart()[1..].Trim());
                }

                if (!query.Search.TrimStart().StartsWith('q'))
                {
                    return StaticResultProvider.GetEmptyQueryResults(_context, _settings, _iconsPath);
                }

                string search = query.Search.TrimStart()[1..].Trim();

                ByteStashClient.ByteStashClient client = GetClient();

                ICollection<ByteStashClient.Snippet> snippets = client.SearchAsync(
                    search,
                    ByteStashClient.Sort.AlphaAsc,
                    _settings.SearchInCode
                ).GetAwaiter().GetResult();

                return [.. snippets.Select(snippet => new Result {
                    Title = snippet.Title,
                    SubTitle = snippet.Description,
                    PreviewPanel = CreatePreviewPanel(snippet),
                    IcoPath =  Path.Combine(_iconsPath, Icon.SNIPPET),
                    ContextData = snippet,
                    Action = _ =>
                    {
                        _context.API.CopyToClipboard(snippet.Fragments.FirstOrDefault()?.Code ?? string.Empty, showDefaultNotification: false);
                        return true;
                    }
                })];

            }
            catch (Exception ex)
            {
                return StaticResultProvider.GetErrorResults(_context, ex, _iconsPath);
            }
        }

        private List<Result> HandleCreateSnippet(string input)
        {
            List<Result> results = [];

            string fullCommand = "+ "
                + Strings.CreateSnippet_Help_SubTitle_Title
                + " | "
                + Strings.CreateSnippet_Help_SubTitle_Description
                + " | "
                + Strings.CreateSnippet_Help_SubTitle_Categories
                + " | "
                + Strings.CreateSnippet_Help_SubTitle_Code;

            if (string.IsNullOrWhiteSpace(input))
            {
                results.Add(new Result
                {
                    Title = Strings.CreateSnippet_Help_Title,
                    SubTitle = fullCommand,
                    IcoPath = Path.Combine(_iconsPath, Icon.NEW_SNIPPET),
                    AutoCompleteText = fullCommand,
                    QuerySuggestionText = fullCommand
                });
                return results;
            }


            string clipboardCode = GetClipboardText();

            var parts = input.Split('|').Select(p => p.Trim()).ToArray();

            string title = parts[0];
            string description = parts.Length > 1 ? parts[1] : string.Empty;
            string categoriesInput = parts.Length > 2 ? parts[2] : string.Empty;
            string explicitCode = parts.Length > 3 ? parts[3] : null;

            List<string> categories = [];
            if (!string.IsNullOrWhiteSpace(categoriesInput))
            {
                categories = [.. categoriesInput
                    .Split(',')
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))];
            }

            string code = !string.IsNullOrWhiteSpace(explicitCode) ? explicitCode : clipboardCode;
            bool hasCode = !string.IsNullOrWhiteSpace(code);

            string previewTitle = !string.IsNullOrWhiteSpace(title) ? title : "// TODO: " + Strings.CreateSnippet_Help_SubTitle_Title;
            string previewDescription = !string.IsNullOrWhiteSpace(description) ? description : "// TODO: " + Strings.CreateSnippet_Help_SubTitle_Description;
            string previewCode = hasCode ? code : "// TODO: " + Strings.CreateSnippet_Help_SubTitle_Code;

            if (hasCode)
            {
                string language = LanguageDetector.DetectLanguage(code);
            }

            string querySuggestionText = GetQuerySuggestionText(input, description, categoriesInput);

            results.Add(new Result
            {
                Title = Strings.CreateSnippet_Help_Title,
                SubTitle = fullCommand,
                AutoCompleteText = querySuggestionText,
                QuerySuggestionText = querySuggestionText,
                IcoPath = Path.Combine(_iconsPath, Icon.NEW_SNIPPET),
                PreviewPanel = CreateSnippetCreationPreview(previewTitle, previewDescription, previewCode, hasCode, categories),
                Action = _ =>
                {
                    return CreateSnippet(title, description, categories, code);
                }
            });

            return results;
        }

        private static string GetQuerySuggestionText(string input, string description, string categoriesInput)
        {
            string querySuggestionText = "+ " + input;
            int progress = querySuggestionText.ToCharArray().Where(c => c == '|').Count();
            if (progress < 1)
            {
                querySuggestionText += " | ";
            }
            if (string.IsNullOrWhiteSpace(description))
            {
                if (querySuggestionText.EndsWith('|'))
                {
                    querySuggestionText += " ";
                }
                querySuggestionText += Strings.CreateSnippet_Help_SubTitle_Description;
            }
            if (progress < 2)
            {
                querySuggestionText += " | ";
            }
            if (string.IsNullOrWhiteSpace(categoriesInput))
            {
                if (querySuggestionText.EndsWith('|'))
                {
                    querySuggestionText += " ";
                }
                querySuggestionText += Strings.CreateSnippet_Help_SubTitle_Categories;
            }
            if (progress < 3)
            {
                querySuggestionText += " | " + Strings.CreateSnippet_Help_SubTitle_Code;
            }

            return querySuggestionText;
        }

        private static string GetClipboardText()
        {

            string clipboardText = null;
            Thread staThread = new(
                delegate ()
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            clipboardText = Clipboard.GetText();
                        }
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
            return clipboardText;
        }

        private Lazy<UserControl> CreateSnippetCreationPreview(string title, string description, string code, bool hasRealCode, List<string> categories)
        {
            return new Lazy<UserControl>(() =>
            {
                try
                {
                    string language = hasRealCode ? LanguageDetector.DetectLanguage(code) : "plaintext";

                    var snippetPreview = new ByteStashClient.Snippet
                    {
                        Title = title,
                        Description = description,
                        Categories = categories ?? [],
                        Fragments =
                        [
                            new ByteStashClient.Fragment
                            {
                                File_name = "main",
                                Code = code,
                                Language = language,
                                Position = 0
                            }
                        ]
                    };

                    var preview = new SnippetPreview();
                    preview.SetSnippet(snippetPreview, _context.API);
                    return preview;
                }
                catch
                {
                    // If preview creation fails, return null to show no preview
                    return null;
                }
            });
        }

        private bool CreateSnippet(string title, string description, List<string> categories, string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    _context.API.ShowMsg(
                        Strings.ContextMenu_Error_Title,
                        Strings.CreateSnippet_Error_EmptyCode
                    );
                    return false;
                }

                string language = LanguageDetector.DetectLanguage(code);
                var fragments = new[]
                {
                    new
                    {
                        file_name = "main",
                        code,
                        language,
                        position = 0
                    }
                };

                string categoriesString = string.Join(",", categories ?? []);

                ByteStashClient.ByteStashClient client = GetClient();
                ByteStashClient.Snippet createdSnippet = client.PushAsync(
                    title,
                    description,
                    false,
                    categoriesString,
                    [],
                    JsonSerializer.Serialize(fragments)
                ).GetAwaiter().GetResult();

                _context.API.ShowMsg(
                    Strings.CreateSnippet_Success_Title,
                    string.Format(Strings.CreateSnippet_Success_Message, title)
                );

                return true;
            }
            catch (Exception ex)
            {
                _context.API.ShowMsg(
                    Strings.ContextMenu_Error_Title,
                    string.Format(Strings.CreateSnippet_Error_CannotCreate, ex.Message)
                );
                return false;
            }
        }

        private Lazy<UserControl> CreatePreviewPanel(ByteStashClient.Snippet snippet)
        {
            return new Lazy<UserControl>(() =>
            {
                var preview = new SnippetPreview();
                preview.SetSnippet(snippet, _context.API);
                return preview;
            });
        }

        /// <summary>
        /// Loads context menu items for a given result.
        /// </summary>
        /// <param name="selectedResult">The selected result.</param>
        /// <returns>A list of context menu items.</returns>
        public List<Result> LoadContextMenus(Result selectedResult)
        {
            List<Result> contextMenus = [];

            if (selectedResult?.ContextData is ByteStashClient.Snippet snippet)
            {
                // Add "Open in ByteStash"
                contextMenus.Add(new Result
                {
                    Title = Strings.ContextMenu_ViewInByteStash,
                    SubTitle = string.Format(Strings.ContextMenu_ViewInByteStash_SubTitle, snippet.Title),
                    IcoPath = Path.Combine(_iconsPath, Icon.EXTERNAL_LINK),
                    Action = _ =>
                    {
                        try
                        {
                            var detailsUrl = $"{_settings.BaseUrl.TrimEnd('/')}/snippets/{snippet.Id}";
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = detailsUrl,
                                UseShellExecute = true
                            });
                            return true;
                        }
                        catch (Exception ex)
                        {
                            _context.API.ShowMsg(
                                Strings.ContextMenu_Error_Title,
                                string.Format(Strings.ContextMenu_Error_CannotOpenByteStash, ex.Message)
                            );
                            return false;
                        }
                    }
                });

                // Add copy for each fragment
                if (snippet.Fragments != null && snippet.Fragments.Count != 0)
                {
                    int fragmentIndex = 1;
                    foreach (var fragment in snippet.Fragments.OrderBy(f => f.Position))
                    {
                        var fragmentTitle = !string.IsNullOrEmpty(fragment.File_name)
                            ? fragment.File_name
                            : string.Format(Strings.ContextMenu_Fragment_DefaultName, fragmentIndex);

                        contextMenus.Add(new Result
                        {
                            Title = string.Format(Strings.ContextMenu_CopyCode, fragmentTitle),
                            SubTitle = string.Format(Strings.ContextMenu_CopyCode_SubTitle, fragment.Language, fragment.Code?.Length ?? 0),
                            IcoPath = Path.Combine(_iconsPath, Icon.CODE),
                            Action = _ =>
                            {
                                _context.API.CopyToClipboard(fragment.Code ?? string.Empty, showDefaultNotification: false);
                                _context.API.ShowMsg(
                                    Strings.ContextMenu_Success_CodeCopied,
                                    string.Format(Strings.ContextMenu_Success_CodeCopied_Fragment, fragmentTitle)
                                );
                                return true;
                            }
                        });

                        fragmentIndex++;
                    }
                }

                // Add delete
                contextMenus.Add(new Result
                {
                    Title = Strings.ContextMenu_Delete,
                    SubTitle = string.Format(Strings.ContextMenu_Delete_SubTitle, snippet.Title),
                    IcoPath = Path.Combine(_iconsPath, Icon.DELETE),
                    Action = _ =>
                    {
                        DeleteSnippet(snippet);
                        return false;
                    }
                });
            }

            return contextMenus;
        }

        private void DeleteSnippet(ByteStashClient.Snippet snippet)
        {
            try
            {
                MessageBoxResult result = _context.API.ShowMsgBox(
                    string.Format(Strings.ContextMenu_Delete_Confirm_Message, snippet.Title),
                    string.Format(Strings.ContextMenu_Delete_Confirm_Title, snippet.Title),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (result != MessageBoxResult.Yes)
                    return;

                ByteStashClient.ByteStashClient client = GetClient();
                client.SnippetsDELETE2Async(snippet.Id).GetAwaiter().GetResult();

                _context.API.ShowMsg(Strings.ContextMenu_Delete_Success_Title, string.Format(Strings.ContextMenu_Delete_Success_Message, snippet.Title));
            }
            catch (Exception ex)
            {
                _context.API.ShowMsg(Strings.ContextMenu_Delete_Error_Title, string.Format(Strings.ContextMenu_Delete_Error_Message, snippet.Title, ex.Message));
            }
        }
    }
}