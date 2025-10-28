using ByteStashClient;
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
using System.Text.RegularExpressions;
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
        private string _iconsPath;
        private Snippet _cachedSnippetForEdit;

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

                if (query.Search.TrimStart().StartsWith('*'))
                {
                    return HandleEditSnippet(query.Search);
                }
                _cachedSnippetForEdit = null; // left edit mode, clear cached snippet

                if (query.Search.TrimStart().StartsWith('+'))
                {
                    return HandleCreateSnippet(query.Search);
                }

                if (!query.Search.TrimStart().StartsWith('q'))
                {
                    return StaticResultProvider.GetEmptyQueryResults(_context, _settings, _iconsPath);
                }

                string search = query.Search.TrimStart()[1..].Trim();

                ByteStashClient.ByteStashClient client = GetClient();

                ICollection<Snippet> snippets = client.SearchAsync(
                    search,
                    Sort.AlphaAsc,
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
                + " " + _settings.CreationQueryDelimiter + " "
                + Strings.CreateSnippet_Help_SubTitle_Description
                + " " + _settings.CreationQueryDelimiter + " "
                + Strings.CreateSnippet_Help_SubTitle_Categories
                + " " + _settings.CreationQueryDelimiter + " "
                + Strings.CreateSnippet_Help_SubTitle_Code
                + " "
                + string.Format(Strings.CreateSnippet_Help_SubTitle_CodeHint, _settings.CreationQueryDelimiter);

            if (string.IsNullOrWhiteSpace(input.TrimStart()[1..])) // exclude the '+' sign
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

            var (title, description, categoriesRaw, categories, codeFragments) = ParseSnippetInput(input);

            // If no code fragments provided, use clipboard content if available
            string clipboardCode = GetClipboardText();
            if (codeFragments.Count == 0 && !string.IsNullOrWhiteSpace(clipboardCode))
            {
                codeFragments = [clipboardCode];
            }
            if (codeFragments.Count == 0)
            {
                codeFragments = GetDefaultCodeFragments();
            }

            string previewTitle = !string.IsNullOrWhiteSpace(title) ? title : GetDefaultTitle();
            string previewDescription = !string.IsNullOrWhiteSpace(description) ? description : GetDefaultDescription();

            string querySuggestionText = GetQuerySuggestionText(input, title, description, categoriesRaw);

            results.Add(new Result
            {
                Title = Strings.CreateSnippet_Help_Title,
                SubTitle = fullCommand,
                AutoCompleteText = querySuggestionText,
                QuerySuggestionText = querySuggestionText,
                IcoPath = Path.Combine(_iconsPath, Icon.NEW_SNIPPET),
                PreviewPanel = CreateSnippetCreationPreview(previewTitle, previewDescription, codeFragments, categories),
                Action = _ =>
                {
                    return CreateSnippet(title, description, categories, codeFragments);
                }
            });

            return results;
        }

        private List<Result> HandleEditSnippet(string input)
        {
            List<Result> results = [];

            if (_cachedSnippetForEdit == null)
            {

                if (string.IsNullOrWhiteSpace(input.TrimStart()[1..])) // exclude the '*' sign
                {
                    results.Add(CreateEditModeHelpResult());
                    return results;
                }

                string search = input.TrimStart()[1..].Trim();
                ByteStashClient.ByteStashClient client = GetClient();

                ICollection<Snippet> snippets = client.SearchAsync(
                    search,
                    Sort.AlphaAsc,
                    _settings.SearchInCode
                ).GetAwaiter().GetResult();

                return [.. snippets.Select(CreateEditSearchResult)];
            }

            return [CreateEditFormResult(input)];
        }

        private Result CreateEditModeHelpResult()
        {
            return new Result
            {
                Title = Strings.EditSnippet_Help_Title,
                SubTitle = Strings.EditSnippet_Help_SubTitle,
                IcoPath = Path.Combine(_iconsPath, Icon.EDIT),
                AutoCompleteText = "* ",
                QuerySuggestionText = "* "
            };
        }

        private Result CreateEditSearchResult(Snippet snippet)
        {
            return new Result
            {
                Title = snippet.Title,
                SubTitle = Strings.EditSnippet_Search_SubTitle,
                PreviewPanel = CreatePreviewPanel(snippet),
                IcoPath = Path.Combine(_iconsPath, Icon.SNIPPET),
                // Don't set ContextData to disable context menu in edit search mode
                Action = _ =>
                {
                    _cachedSnippetForEdit = snippet;
                    string query = BuildEditQuery(snippet);
                    _context.API.ChangeQuery(query, true);
                    return false;
                }
            };
        }

        private Result CreateEditFormResult(string input)
        {
            string fullCommand = "* "
                  + Strings.CreateSnippet_Help_SubTitle_Title
                  + " " + _settings.CreationQueryDelimiter + " "
                  + Strings.CreateSnippet_Help_SubTitle_Description
                  + " " + _settings.CreationQueryDelimiter + " "
                  + Strings.CreateSnippet_Help_SubTitle_Categories
                  + " " + _settings.CreationQueryDelimiter + " "
                  + Strings.CreateSnippet_Help_SubTitle_Code
                  + " "
                  + string.Format(Strings.CreateSnippet_Help_SubTitle_CodeHint, _settings.CreationQueryDelimiter);

            var (title, description, categoriesRaw, categories, codeFragments) = ParseSnippetInput(input);
            if (codeFragments.Count == 0)
            {
                codeFragments = GetDefaultCodeFragments();
            }

            string previewTitle = !string.IsNullOrWhiteSpace(title) ? title : (_cachedSnippetForEdit.Title ?? GetDefaultTitle());
            string previewDescription = !string.IsNullOrWhiteSpace(description) ? description : (_cachedSnippetForEdit.Description ?? GetDefaultDescription());

            string querySuggestionText = GetQuerySuggestionText(input, title, description, categoriesRaw);

            return new Result
            {
                Title = Strings.EditSnippet_Help_Title,
                SubTitle = fullCommand,
                AutoCompleteText = querySuggestionText,
                QuerySuggestionText = querySuggestionText,
                IcoPath = Path.Combine(_iconsPath, Icon.EDIT),
                PreviewPanel = CreateSnippetCreationPreview(previewTitle, previewDescription, codeFragments, categories),
                Action = _ =>
                {
                    bool result = UpdateSnippet(title, description, categories, codeFragments);
                    if (result)
                    {
                        _cachedSnippetForEdit = null;
                    }
                    return result;
                }
            };
        }

        private (string title, string description, string categoriesRaw, List<string> categories, List<string> codeFragments) ParseSnippetInput(string input)
        {
            string[] parts = [..
                Regex.Split(input.TrimStart()[1..], GetDelimiterRegex()) // remove command sign
                    .Where((value, index) => index % 2 == 0) // remove regex matches
                    .Select(p => p.Trim())
            ];

            string title = parts.Length > 0 ? parts[0] : string.Empty;
            string description = parts.Length > 1 ? parts[1] : string.Empty;
            string categoriesRaw = parts.Length > 2 ? parts[2] : string.Empty;

            List<string> categories = [];
            if (!string.IsNullOrWhiteSpace(categoriesRaw))
            {
                categories = [.. categoriesRaw
                    .Split(',')
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))];
            }

            List<string> codeFragments = parts.Length > 3 ? [.. parts.Skip(3)] : [];

            return (title, description, categoriesRaw, categories, codeFragments);
        }

        private string BuildEditQuery(Snippet snippet)
        {

            string title = snippet.Title ?? GetDefaultTitle();
            string description = snippet.Description ?? GetDefaultDescription();
            string categories = snippet.Categories != null && snippet.Categories.Count > 0
                ? string.Join(", ", snippet.Categories)
                : string.Empty;

            List<string> codeFragments = snippet.Fragments?
                .OrderBy(f => f.Position)
                .Select(f => f.Code ?? string.Empty)
                .ToList() ?? [];

            string query = "stash * " + title;
            if (!string.IsNullOrWhiteSpace(description) || !string.IsNullOrWhiteSpace(categories) || codeFragments.Count > 0)
            {
                query += " " + _settings.CreationQueryDelimiter + " " + description;
            }
            if (!string.IsNullOrWhiteSpace(categories) || codeFragments.Count > 0)
            {
                query += " " + _settings.CreationQueryDelimiter + " " + categories;
            }
            if (codeFragments.Count > 0)
            {
                query += " " + _settings.CreationQueryDelimiter + " " + string.Join(" " + _settings.CreationQueryDelimiter + " ", codeFragments);
            }

            return query;
        }

        private bool UpdateSnippet(string title, string description, List<string> categories, List<string> codeFragments)
        {
            try
            {
                if (_cachedSnippetForEdit == null)
                {
                    _context.API.ShowMsg(
                        Strings.ContextMenu_Error_Title,
                        Strings.EditSnippet_Error_NoSnippetSelected
                    );
                    return false;
                }

                if (codeFragments.Count == 0)
                {
                    _context.API.ShowMsg(
                        Strings.ContextMenu_Error_Title,
                        Strings.CreateSnippet_Error_EmptyCode
                    );
                    return false;
                }

                string fragmentString = GetSerialized(codeFragments);

                string categoriesString = string.Join(",", categories ?? []);

                ByteStashClient.ByteStashClient client = GetClient();

                client.SnippetsPUT2Async(_cachedSnippetForEdit.Id, title, description, false, categoriesString, [], fragmentString).GetAwaiter().GetResult();

                _context.API.ShowMsg(
                    Strings.EditSnippet_Success_Title,
                    string.Format(Strings.EditSnippet_Success_Message, title)
                );

                return true;

            }
            catch (Exception ex)
            {
                _context.API.ShowMsg(
                    Strings.ContextMenu_Error_Title,
                    string.Format(Strings.EditSnippet_Error_CannotUpdate, ex.Message)
                );
                return false;
            }
        }

        private string GetQuerySuggestionText(string input, string title, string description, string categoriesInput)
        {
            string querySuggestionText = input;
            int progress = Regex.Matches(input, GetDelimiterRegex()).Count;
            if (progress < 1)
            {
                if (string.IsNullOrWhiteSpace(title))
                {
                    if (!querySuggestionText.EndsWith(' '))
                        querySuggestionText += " ";

                    querySuggestionText += Strings.CreateSnippet_Help_SubTitle_Title;
                }
                querySuggestionText += " " + _settings.CreationQueryDelimiter + " ";
            }
            if (progress < 2)
            {
                if (string.IsNullOrWhiteSpace(description))
                {
                    if (!querySuggestionText.EndsWith(' '))
                        querySuggestionText += " ";

                    querySuggestionText += Strings.CreateSnippet_Help_SubTitle_Description;
                }
                querySuggestionText += " " + _settings.CreationQueryDelimiter + " ";
            }
            if (progress < 3)
            {
                if (string.IsNullOrWhiteSpace(categoriesInput))
                {
                    if (!querySuggestionText.EndsWith(' '))
                        querySuggestionText += " ";

                    querySuggestionText += Strings.CreateSnippet_Help_SubTitle_Categories;
                }
                querySuggestionText += " "
                    + _settings.CreationQueryDelimiter
                    + " "
                    + Strings.CreateSnippet_Help_SubTitle_Code;
            }
            if (!querySuggestionText.EndsWith(' '))
                querySuggestionText += " ";
            querySuggestionText += string.Format(Strings.CreateSnippet_Help_SubTitle_CodeHint, _settings.CreationQueryDelimiter);

            return querySuggestionText;
        }

        private string GetDelimiterRegex()
        {
            string escapedDelimiter = Regex.Escape(_settings.CreationQueryDelimiter);
            return string.Format(@"(?<=\s){0}(\s|$)", escapedDelimiter);
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

        private Lazy<UserControl> CreateSnippetCreationPreview(string title, string description, List<string> codeFragments, List<string> categories)
        {
            return new Lazy<UserControl>(() =>
            {
                try
                {
                    List<Fragment> fragments = ToFragment(codeFragments);

                    Snippet snippetPreview = new()
                    {
                        Title = title,
                        Description = description,
                        Categories = categories ?? [],
                        Fragments = fragments
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

        private bool CreateSnippet(string title, string description, List<string> categories, List<string> codeFragments)
        {
            try
            {
                if (codeFragments.Count == 0)
                {
                    _context.API.ShowMsg(
                        Strings.ContextMenu_Error_Title,
                        Strings.CreateSnippet_Error_EmptyCode
                    );
                    return false;
                }

                string fragmentString = GetSerialized(codeFragments);

                string categoriesString = string.Join(",", categories ?? []);

                ByteStashClient.ByteStashClient client = GetClient();
                client.PushAsync(
                    !string.IsNullOrWhiteSpace(title) ? title : GetDefaultTitle(),
                    !string.IsNullOrWhiteSpace(description) ? description : GetDefaultDescription(),
                    false,
                    categoriesString,
                    [],
                    fragmentString
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

        private static string GetSerialized(List<string> codeFragments)
        {
            return JsonSerializer.Serialize(ToFragment(codeFragments).Select((fragment) => new
            {
                position = fragment.Position,
                file_name = fragment.File_name,
                language = fragment.Language,
                code = fragment.Code
            }));
        }

        private static List<Fragment> ToFragment(List<string> codeFragments)
        {
            return [.. codeFragments.Select((code, index) =>
                    {
                        string language = LanguageDetector.DetectLanguage(code);
                        return new Fragment
                        {
                            Position = index,
                            File_name = "fragment_" + (index + 1),
                            Language = language,
                            Code = code
                        };
                    })];
        }

        private static string GetDefaultTitle()
        {
            return "// TODO: " + Strings.CreateSnippet_Help_SubTitle_Title;
        }
        private static string GetDefaultDescription()
        {
            return "// TODO: " + Strings.CreateSnippet_Help_SubTitle_Description;
        }

        private static List<string> GetDefaultCodeFragments()
        {
            return ["// TODO: " + Strings.CreateSnippet_Help_SubTitle_Code];
        }


        private Lazy<UserControl> CreatePreviewPanel(Snippet snippet)
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

            if (selectedResult?.ContextData is Snippet snippet)
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

                // Add edit option
                contextMenus.Add(new Result
                {
                    Title = Strings.ContextMenu_Edit,
                    SubTitle = string.Format(Strings.ContextMenu_Edit_SubTitle, snippet.Title),
                    IcoPath = Path.Combine(_iconsPath, Icon.EDIT),
                    Action = _ =>
                    {
                        _cachedSnippetForEdit = snippet;
                        string query = BuildEditQuery(snippet);
                        _context.API.BackToQueryResults();
                        _context.API.ChangeQuery(query, false);
                        return false; // Don't close Flow Launcher
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