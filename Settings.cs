namespace Flow.Launcher.Plugin.ByteStash
{

    /// <summary>
    /// Represents configuration settings for connecting to the ByteStash API v1.0.0.
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// The base URL for the API requests.
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// The API key used for authenticating requests.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the search operation should include searching in the code.
        /// </summary>
        public bool SearchInCode { get; set; } = false;
    }
}