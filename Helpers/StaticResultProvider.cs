using Flow.Launcher.Plugin.ByteStash.Resources;
using System;
using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Plugin.ByteStash.Helpers
{

    /// <summary>
    /// Provides static methods to generate predefined result sets for various plugin scenarios.
    /// </summary>
    /// <remarks>This class includes methods to create result sets for invalid settings, empty queries, and
    /// error scenarios. The results are tailored to guide users with appropriate actions or feedback based on the
    /// context.</remarks>
    internal class StaticResultProvider
    {

        internal static List<Result> GetInvalidSettingsResults(PluginInitContext context, string iconsPath)
        {
            return [
                 new Result {
                     Title = Strings.Query_Error_MissingSettings_Title,
                     SubTitle = Strings.Query_Error_MissingSettings_SubTitle,
                     IcoPath = Path.Combine(iconsPath, Icon.DISCONNECT),
                     Action = _ =>
                     {
                         context.API.OpenSettingDialog();
                         return true;
                     }
                 }
            ];
        }

        internal static List<Result> GetEmptyQueryResults(PluginInitContext context, Settings settings, string iconsPath)
        {
            return [
                new Result {
                    Title = Strings.Query_HintSearch_Title,
                    SubTitle = Strings.Query_HintSearch_SubTitle,
                    IcoPath = Path.Combine(iconsPath, Icon.SEARCH),
                    Action = _ => {
                        context.API.ChangeQuery("stash q ");
                        return false;
                    }
                },
                new Result {
                    Title = Strings.Query_HintCreate_Title,
                    SubTitle = Strings.Query_HintCreate_SubTitle,
                    IcoPath = Path.Combine(iconsPath, Icon.NEW_SNIPPET),
                    Action = _ => {
                        context.API.ChangeQuery("stash + ");
                        return false;
                    }
                },
                new Result {
                    Title = Strings.Query_HintOpenUrl_Title,
                    SubTitle = Strings.Query_HintOpenUrl_SubTitle,
                    IcoPath = Path.Combine(iconsPath, Icon.EXTERNAL_LINK),
                    Action = _ => {
                        try {
                            var url = settings.BaseUrl;
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                            return true;
                        } catch (Exception ex) {
                            context.API.ShowMsg(
                            Strings.ContextMenu_Error_Title,
                            string.Format(Strings.ContextMenu_Error_CannotOpenByteStash, ex.Message));
                            return false;
                        }
                    }
                }
            ];
        }

        internal static List<Result> GetErrorResults(PluginInitContext context, Exception ex, string iconsPath)
        { 
            return [
                new Result
                {
                    Title = Strings.Query_Error_Connecting_Title,
                    SubTitle = string.Format(Strings.Query_Error_Connecting_SubTitle, ex.Message),
                    IcoPath = Path.Combine(iconsPath, Icon.DISCONNECT),
                    Action = _ =>
                    {
                        context.API.OpenSettingDialog();
                        return true;
                    }
                }
            ];
        }
    }
}