namespace BoardWalk.Bot
{
    public class BotOptions
    {
        /// <summary>Where the BoardWalk API listens, e.g. http://localhost:5199</summary>
        public string ApiBaseUrl { get; set; } = "http://localhost:5199";

        /// <summary>Shared secret; must equal Bot:ApiKey in the API's configuration.</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Pause after each action so humans can follow what the bot is doing.</summary>
        public int ThinkDelayMs { get; set; } = 1500;

        /// <summary>How often to ask the API whether any bot seat needs to act.</summary>
        public int PollIntervalMs { get; set; } = 1000;
    }
}
