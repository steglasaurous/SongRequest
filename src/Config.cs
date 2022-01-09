using MelonLoader;
using System.Reflection;

namespace AudicaModding
{
    internal static class Config
    {
        public const string Category = "SongRequest";

        public static bool LetModsIgnoreQueueStatus;
        public static bool LetModsChangeQueueStatus;
        public static bool LetModsRemoveRequests;

        public static bool AutomaticallyRemoveSongs;

        public static string TwitchUsername;
        public static string TwitchAccessToken;
        public static string TwitchChannel;


        public static void RegisterConfig()
        {
            MelonPreferences.CreateEntry(Category, nameof(AutomaticallyRemoveSongs), true,
                                    "Automatically remove a request if it has been played.");

            MelonPreferences.CreateEntry(Category, nameof(LetModsIgnoreQueueStatus), true,
                                    "Allows mods to add songs to the queue even if queue is closed.");

            MelonPreferences.CreateEntry(Category, nameof(LetModsChangeQueueStatus), true,
                                    "Allows mods to open/close queue.");

            MelonPreferences.CreateEntry(Category, nameof(LetModsRemoveRequests), true,
                                    "Allows mods to remove requests from the queue.");

            MelonPreferences.CreateEntry(Category, nameof(TwitchUsername), "",
                                    "Twitch Username (used for writing messages back to twitch chat)");

            MelonPreferences.CreateEntry(Category, nameof(TwitchAccessToken), "",
                                    "Twitch OAuth access token (used for writing messages back to twitch chat)");
            
            MelonPreferences.CreateEntry(Category, nameof(TwitchChannel), "",
                                    "Twitch channel to listen and post messages to.");

            OnModSettingsApplied();
        }

        public static void OnModSettingsApplied()
        {
            foreach (var fieldInfo in typeof(Config).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (fieldInfo.FieldType == typeof(bool))
                    fieldInfo.SetValue(null, MelonPreferences.GetEntryValue<bool>(Category, fieldInfo.Name));
                if (fieldInfo.FieldType == typeof(string) && fieldInfo.Name != nameof(Category))
                    fieldInfo.SetValue(null, MelonPreferences.GetEntryValue<string>(Category, fieldInfo.Name));
            }
        }
    }
}
