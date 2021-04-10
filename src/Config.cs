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

        public static void RegisterConfig()
        {
            MelonPrefs.RegisterBool(Category, nameof(LetModsIgnoreQueueStatus), true,
                                    "Allows mods to add songs to the queue even if queue is closed.");

            MelonPrefs.RegisterBool(Category, nameof(LetModsChangeQueueStatus), true,
                                    "Allows mods to open/close queue.");

            MelonPrefs.RegisterBool(Category, nameof(LetModsRemoveRequests), true,
                                    "Allows mods to remove requests from the queue.");

            OnModSettingsApplied();
        }

        public static void OnModSettingsApplied()
        {
            foreach (var fieldInfo in typeof(Config).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (fieldInfo.FieldType == typeof(bool))
                    fieldInfo.SetValue(null, MelonPrefs.GetBool(Category, fieldInfo.Name));
            }
        }
    }
}
