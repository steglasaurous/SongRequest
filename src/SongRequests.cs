using MelonLoader;
using MelonLoader.TinyJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AudicaWebsocketServer;

[assembly: MelonOptionalDependencies("SongBrowser", "ModSettings", "AudicaWebsocketServer", "TwitchConnectorMod")]

namespace AudicaModding
{
    public class SongRequests : MelonMod
    {
        public static class BuildInfo
        {
            public const string Name = "SongRequest";  // Name of the Mod.  (MUST BE SET)
            public const string Author = "Alternity and Silzoid"; // Author of the Mod.  (Set as null if none)
            public const string Company = null; // Company that made the Mod.  (Set as null if none)
            public const string Version = "2.1.0"; // Version of the Mod.  (MUST BE SET)
            public const string DownloadLink = null; // Download Link for the Mod.  (Set as null if none)
        }

        internal static bool loadComplete             = false;
        internal static bool hasCompatibleSongBrowser = false;
        internal static bool hasCompatibleWebsocketServer = false;
        internal static bool hasCompatibleTwitchConnectorMod = false;

        internal static bool requestsEnabled = true;

        private static List<RequestData> unprocessedRequests = new List<RequestData>();
        private static RequestQueue      requests            = new RequestQueue();

        private static Dictionary<string, QueryData> webSearchQueryData = new Dictionary<string, QueryData>();
        private static string                        queuePath          = UnityEngine.Application.dataPath + "/../" + "/UserData/" + "SongRequestQueue.json";

        public override void OnApplicationStart()
        {
            if (MelonHandler.Mods.Any(HasCompatibleSongBrowser))
            {
                InitSongBrowserIntegration();
            }
            else
            {
                MelonLogger.Msg("No compatible version of SongBrowser found. Searching for and downloading of missing songs is disabled.");
            }

            if (MelonHandler.Mods.Any(HasCompatibleWebsocketServerMod))
            {
                InitWebsocketServerIntegration();
            }
            else
            {
                MelonLogger.Msg("No compatible version of AudicaWebsocketServer found.  Websocket messages for bot responses disabled.");
            }

            if (MelonHandler.Mods.Any(HasCompatibleTwitchConnectorMod))
            {
                InitTwitchConnectorModIntegration();
            } else
            {
                MelonLogger.Msg("No compatible version of TwitchConnectorMod found.  Twitch integration will not be available for bot commands (like !asr)");
            }

            if (MelonHandler.Mods.Any(HasCompatibleWebsocketServerMod))
            {
                InitWebsocketServerIntegration();
            }
            else
            {
                MelonLogger.Msg("No compatible version of AudicaWebsocketServer found.  Websocket messages for bot responses disabled.");
            }

            Config.RegisterConfig();
        }

        public override void OnPreferencesSaved()
        {
            Config.OnModSettingsApplied();
        }

        public static int GetActiveWebSearchCount()
        {
            return webSearchQueryData.Count;
        }

        private void InitSongBrowserIntegration()
        {
            hasCompatibleSongBrowser = true;
            MelonLogger.Msg("Song Browser is installed. Enabling integration");
            RequestUI.Register();

            // make sure queue is processed after song list reload
            // to show requested maps that were missing and just
            // got downloaded
            SongBrowser.RegisterSongListPostProcessing(ProcessQueueAfterRefresh);
        }

        private void InitWebsocketServerIntegration()
        {
            hasCompatibleWebsocketServer = true;
            MelonLogger.Msg("Websocket Server is installed. Enabling websocket messages for bot requests.");
        }

        private void InitTwitchConnectorModIntegration()
        {
            hasCompatibleTwitchConnectorMod = true;
            TwitchConnectorMod.TwitchConnectorMod.AddChatMsgReceivedEventHandler(OnChatMessage);
            MelonLogger.Msg("TwitchConnectorMod is installed.  Twitch commands and responses enabled.");
        }

        void OnChatMessage(Object sender, TwitchConnectorMod.ParsedTwitchMessage eventArgs)
        {
            // Technically this is arriving as a very similar object, however to avoid putting a hard dependency on TwitchConnectorMod,
            // we parse the message here too.
            ParseCommand(new ParsedTwitchMessage(eventArgs.RawMessage));
        }

        internal static void ProcessBuiltinTwitchMessage(string msg)
        {
            if (hasCompatibleTwitchConnectorMod)
            {
                // If the twitch connector mod is enabled and the Audica built-in twitch is functional, ignore messages
                // from it so we don't double-up on messages to process.  
                return;
            }

            SongRequests.ParseCommand(new ParsedTwitchMessage(msg));
        }

        private bool HasCompatibleSongBrowser(MelonMod mod)
        {
            if (mod.Info.SystemType.Name == nameof(SongBrowser))
            {
                Version browserVersion       = new Version(mod.Info.Version);
                Version lastSupportedVersion = new Version("3.0.8");
                return browserVersion.CompareTo(lastSupportedVersion) >= 0;
            }
            return false;
        }

        private bool HasCompatibleWebsocketServerMod(MelonMod mod)
        {
            if (mod.Info.SystemType.Name == nameof(AudicaWebsocketServerMain))
            {
                Version websocketServerVersion = new Version(mod.Info.Version);
                Version lastSupportedVersion = new Version("1.1.0");
                return websocketServerVersion.CompareTo(lastSupportedVersion) >= 0;
            }
            return false;
        }

        private bool HasCompatibleTwitchConnectorMod(MelonMod mod)
        {
            if (mod.Info.SystemType.Name == nameof(TwitchConnectorMod))
            {
                Version twitchConnectorVersion = new Version(mod.Info.Version);
                Version lastSupportedVersion = new Version("0.1.0");
                return twitchConnectorVersion.CompareTo(lastSupportedVersion) >= 0;
            }
            return false;
        }

        public static int GetBits(ParsedTwitchMessage msg)
        {
            if (msg.Bits == "")
            {
                return 0;
            }
            else
            {
                int totalBits = 0;
                foreach (string str in msg.Bits.Split(",".ToCharArray()))
                {
                    totalBits += Convert.ToInt32(str);
                }
                return totalBits;
            }
        }

        private static SongList.SongData SearchSong(QueryData data, out bool foundExactMatch)
        {
            SongList.SongData song        = null;
            bool              foundAny    = false;
            bool              foundBetter = false;
            foundExactMatch               = false;

            for (int i = 0; i < SongList.I.songs.Count; i++)
            {
                SongList.SongData currentSong = SongList.I.songs[i];
                SongInfo match = new SongInfo
                {
                    SongID = currentSong.songID,
                    Title  = currentSong.title,
                    Artist = currentSong.artist,
                    Mapper = currentSong.author
                };

                if (LookForMatch(data, match, ref foundAny, ref foundBetter, ref foundExactMatch))
                {
                    song = currentSong;
                    if (foundExactMatch)
                        break;
                }
            }
            return song;
        }
        private static bool LookForMatch(QueryData query, SongInfo match,
                                         ref bool foundAny, ref bool foundBetter, ref bool foundExact)
        {
            if (query.MaudicaID != null) // if we searched via Maudica ID then we know the match is exact
            {
                foundAny    = true;
                foundBetter = true;
                foundExact  = true;
                return true;
            }

            bool hasArtist = match.Artist == null ||
                             query.Artist == null || match.Artist.ToLowerInvariant().Replace(" ", "").Contains(query.Artist);
            bool hasMapper = match.Mapper == null ||
                             query.Mapper == null || match.Mapper.ToLowerInvariant().Replace(" ", "").Contains(query.Mapper);
            bool hasTitle  = match.Title.ToLowerInvariant().Contains(query.Title) ||
                             match.SongID.ToLowerInvariant().Contains(query.Title.Replace(" ", ""));

            if ((hasArtist && hasMapper && hasTitle) ||
                (query.Title == "" && query.Artist != null && hasArtist) ||
                (query.Title == "" && query.Mapper != null && hasMapper))
            {
                return LookForMatch(query.Title, match.Title, ref foundAny, ref foundBetter, ref foundExact);
            }
            return false;
        }
        private static bool LookForMatch(string querySongTitle, string matchSongTitle,
                                         ref bool foundAny, ref bool foundBetter, ref bool foundExact)
        {
            bool newBestMatch = false;
            // keep first partial match as result unless we find an exact match
            if (!foundAny)
            {
                foundAny      = true;
                newBestMatch  = true;
            }

            // prefer songs that actually start with the first word of the query
            // over random partial matches (e.g. !asr Bang should find Bang! and not
            // My New Sneakers Could Never Replace My Multi-Colored Bangalores
            if (!foundBetter)
            {
                if (matchSongTitle.ToLowerInvariant().StartsWith(querySongTitle))
                {
                    foundBetter  = true;
                    newBestMatch = true;
                }
            }

            // exact matches are best
            if (matchSongTitle.ToLowerInvariant().Trim().Equals(querySongTitle))
            {
                foundExact   = true;
                newBestMatch = true;
            }

            return newBestMatch;
        }

        public static void ProcessQueue()
        {
            bool addedAny = false;
            MelonLogger.Msg(unprocessedRequests.Count + " in queue.");

            if (unprocessedRequests.Count != 0)
            {
                foreach (RequestData req in unprocessedRequests)
                {
                    QueryData         data   = new QueryData(req);

                    if (data.MaudicaID != null)
                    {
                        // special case, we definitely need a web-query to identify this one
                        if (hasCompatibleSongBrowser)
                        {
                            SearchID(data);
                        }
                        else
                        {
                            MelonLogger.Msg("Unable to request by Maudica.com song ID without compatible Song Browser mod.");
                        }
                    }
                    else
                    {
                        SongList.SongData result = SearchSong(data, out bool foundExactMatch);

                        if ((!hasCompatibleSongBrowser || foundExactMatch) && result != null)
                        {
                            // if we have web search we want to make sure we prioritize exact matches
                            // over partial local ones
                            MelonLogger.Msg("Result: " + result.songID);
                            if (AddRequest(result, data.RequestedBy, data.RequestedAt))
                                addedAny = true;
                        }
                        else if (hasCompatibleSongBrowser)
                        {
                            StartWebSearch(data);
                        }
                        else
                        {
                            MelonLogger.Msg($"Found no match for \"{req.Query}\"");
                            EmitMessage("SongNotFound", req, req.Query);
                        }
                    }
                }
                unprocessedRequests.Clear();
            }

            if (addedAny && MenuState.GetState() == MenuState.State.SongPage)
                RequestUI.UpdateFilter();

            RequestUI.UpdateButtonText();
        }
        private static void SearchID(QueryData data)
        {
            string search = data.MaudicaID;
            if (!webSearchQueryData.ContainsKey(search))
            {
                webSearchQueryData.Add(search, data);
            }
            MelonCoroutines.Start(SongDownloader.DoMaudicaIDSearch(search, ProcessWebSearchResult));
        }
        private static void StartWebSearch(QueryData data)
        {
            string search = data.Title;
            if (search == "" && !string.IsNullOrEmpty(data.Artist))
            {
                search = data.Artist;
            }
            else if (search == "" && !string.IsNullOrEmpty(data.Mapper))
            {
                search = data.Mapper;
            }
            if (!webSearchQueryData.ContainsKey(search))
            {
                webSearchQueryData.Add(search, data);
            }
            MelonCoroutines.Start(SongDownloader.DoSongWebSearch(search, ProcessWebSearchResult, DifficultyFilter.All));
        }
        private static void ProcessWebSearchResult(string query, APISongList response)
        {
            QueryData data = webSearchQueryData[query];
            bool addedLocalMatch = false;
            if (response.song_count > 0)
            {
                Song bestMatch   = null;
                bool foundAny    = false;
                bool foundBetter = false;
                bool foundExact  = false;
                foreach (Song s in response.songs)
                {
                    SongInfo info = new SongInfo()
                    {
                        SongID = s.song_id,
                        Title  = s.title,
                        Artist = s.artist,
                        Mapper = s.author
                    };

                    if (LookForMatch(data, info, ref foundAny, ref foundBetter, ref foundExact))
                    {
                        bestMatch = s;
                        if (foundExact)
                            break;
                    }
                }
                if (bestMatch != null)
                {
                    // check if we already have that file downloaded
                    RequestData       r         = new RequestData($"{bestMatch.title} -artist {bestMatch.artist} -mapper {bestMatch.author}",
                                                                  data.RequestedBy, data.RequestedAt);
                    QueryData         matchData = new QueryData(r);
                    SongList.SongData s         = SearchSong(matchData, out bool isExactMatch);
                    if (isExactMatch)
                    {
                        MelonLogger.Msg("Result: " + s.songID);
                        if (AddRequest(s, data.RequestedBy, data.RequestedAt))
                            addedLocalMatch = true;
                    }
                    else if (AddMissing(bestMatch, data.RequestedBy, data.RequestedAt))
                    {
                        MelonLogger.Msg("Result (missing): " + bestMatch.song_id);
                    }
                }
                else
                {
                    MelonLogger.Msg($"Found no match for \"{data.FullQuery}\"");
                    EmitMessage("SongNotFound", data, data.FullQuery);
                }
            }
            else
            {
                // check if we have a local match (can happen if
                // this particular map hasn't been uploaded or was taken down)
                SongList.SongData s = SearchSong(data, out bool _);
                if (s != null)
                {
                    MelonLogger.Msg("Result: " + s.songID);
                    if (AddRequest(s, data.RequestedBy, data.RequestedAt))
                        addedLocalMatch = true;
                }
                else
                {
                    MelonLogger.Msg($"Found no match for \"{data.FullQuery}\"");
                    EmitMessage("SongNotFound", data, data.FullQuery);
                }
            }

            if (addedLocalMatch && MenuState.GetState() == MenuState.State.SongPage)
                RequestUI.UpdateFilter();

            webSearchQueryData.Remove(query);
            if (GetActiveWebSearchCount() == 0)
            {
                RequestUI.UpdateButtonText();
            }
        }
        private static void ProcessQueueAfterRefresh()
        {
            // put all missing songs into the queue to make sure
            // we catch it if they just got downloaded
            foreach (MissingRequest s in requests.MissingSongs)
            {
                unprocessedRequests.Add(new RequestData($"{s.Title} -artist {s.Artist} -mapper {s.Mapper}", s.RequestedBy, s.RequestedAt));
            }
            ClearMissing();

            ProcessQueue();
        }

        public static void ProcessRemoval(string arguments)
        {
            QueryData query = new QueryData(new RequestData(arguments, "", DateTime.Now));

            bool foundAny        = false;
            bool foundBetter     = false;
            bool foundExactMatch = false;
            int  matchIdx        = -1;

            // check available songs
            for (int i = 0; i < requests.AvailableSongs.Count; i++)
            {
                Request req = requests.AvailableSongs[i];

                if (LookForMatch(query, new SongInfo(req), ref foundAny, ref foundBetter, ref foundExactMatch))
                {
                    matchIdx = i;
                    if (foundExactMatch)
                        break;
                }
            }
            if (matchIdx != -1)
            {
                EmitMessage("RemoveSongQueueItem", requests.AvailableSongs[matchIdx], requests.AvailableSongs[matchIdx].Title);
                RemoveRequest(requests.AvailableSongs[matchIdx]);
                MelonLogger.Msg("Removed \"" + arguments + "\" from available requests");
                if (MenuState.GetState() == MenuState.State.SongPage)
                {
                    RequestUI.UpdateFilter();
                }
            }
            else // not in available, might be in missing
            {
                for (int i = 0; i < requests.MissingSongs.Count; i++)
                {
                    MissingRequest req = requests.MissingSongs[i];

                    if (LookForMatch(query, new SongInfo(req), ref foundAny, ref foundBetter, ref foundExactMatch))
                    {
                        matchIdx = i;
                        if (foundExactMatch)
                            break;
                    }
                }
                if (matchIdx != -1)
                {
                    EmitMessage("RemoveSongQueueItem", requests.MissingSongs[matchIdx], requests.MissingSongs[matchIdx].Title);
                    RemoveMissing(requests.MissingSongs[matchIdx]);
                    MelonLogger.Msg("Removed \"" + arguments + "\" from missing requests");
                }
            }
            if (matchIdx != -1 && MenuState.GetState() == MenuState.State.SongPage)
            {
                RequestUI.UpdateButtonText();
            }
        }

        public static void ProcessOops(string userId)
        {
            int availableMatchIdx = -1;
            int missingMatchIdx   = -1;

            // check available songs
            for (int i = requests.AvailableSongs.Count - 1; i >= 0; i--)
            {
                Request req = requests.AvailableSongs[i];
                MelonLogger.Msg($"{userId} vs {req.RequestedBy}");
                if (req.RequestedBy == userId)
                {
                    availableMatchIdx = i;
                    break;
                }
            }
            // check missing songs
            for (int i = requests.MissingSongs.Count - 1; i >= 0; i--)
            {
                MissingRequest req = requests.MissingSongs[i];
                MelonLogger.Msg($"{userId} vs {req.RequestedBy}");

                if (req.RequestedBy == userId)
                {
                    missingMatchIdx = i;
                    break;
                }
            }
            if (availableMatchIdx != -1 || missingMatchIdx != -1)
            {
                // we need the latest request
                Request        available = (availableMatchIdx == -1 ? null : requests.AvailableSongs[availableMatchIdx]);
                MissingRequest missing   = (missingMatchIdx   == -1 ? null : requests.MissingSongs[missingMatchIdx]);

                if (available != null && missing != null)
                {
                    if (available.RequestedAt > missing.RequestedAt)
                    {
                        MelonLogger.Msg($"Removed {userId}'s latest request {available.Title} from available requests");
                        RemoveRequest(available);
                        EmitMessage("RemoveSongQueueItem", available, available.Title);

                        if (MenuState.GetState() == MenuState.State.SongPage)
                        {
                            RequestUI.UpdateFilter();
                        }
                    }
                    else
                    {
                        MelonLogger.Msg($"Removed {userId}'s latest request {missing.Title} from missing requests");
                        RemoveMissing(missing);
                        EmitMessage("RemoveSongQueueItem", missing, missing.Title);
                    }
                }
                else if (available != null)
                {
                    MelonLogger.Msg($"Removed {userId}'s latest request {available.Title} from available requests");
                    RemoveRequest(available);

                    EmitMessage("RemoveSongQueueItem", available, available.Title);
                    if (MenuState.GetState() == MenuState.State.SongPage)
                    {
                        RequestUI.UpdateFilter();
                    }
                }
                else
                {
                    MelonLogger.Msg($"Removed {userId}'s latest request {missing.Title} from missing requests");
                    RemoveMissing(missing);
                    EmitMessage("RemoveSongQueueItem", missing, missing.Title);
                }

                if (MenuState.GetState() == MenuState.State.SongPage)
                {
                    RequestUI.UpdateButtonText();
                }
            }
        }

        public static void ParseCommand(ParsedTwitchMessage twitchMessage)
        {
            string msg = twitchMessage.Message;

            if (msg.Length > 2 && msg.Substring(0, 1) == "!") // length has to be at least 2: ! and at least one command letter
            {
                string command   = msg.Replace("!", "").Split(" ".ToCharArray())[0];
                string arguments = msg.Replace("!" + command + " ", "");
                command          = command.ToLower();

                if (command == "asr" && (requestsEnabled || twitchMessage.Mod == "1" && Config.LetModsIgnoreQueueStatus || twitchMessage.Broadcaster == "1"))
                {
                    MelonLogger.Msg("!asr requested with query \"" + arguments + "\"");

                    unprocessedRequests.Add(new RequestData(arguments, twitchMessage.UserId, DateTime.Now));

                    if (loadComplete)
                    {
                        ProcessQueue();
                    }
                }
                else if ((command == "remove" || command == "yeet") && (twitchMessage.Mod == "1" && Config.LetModsRemoveRequests || twitchMessage.Broadcaster == "1"))
                {
                    MelonLogger.Msg("!remove requested with query \"" + arguments + "\"");
                    ProcessRemoval(arguments);
                }
                else if (command == "oops")
                {
                    MelonLogger.Msg("!oops requested");
                    ProcessOops(twitchMessage.UserId);
                }
                else if (command == "enablequeue" && requestsEnabled == false &&
                         (Config.LetModsChangeQueueStatus && twitchMessage.Mod == "1" || twitchMessage.Broadcaster == "1"))
                {
                    RequestUI.EnableQueue(true);
                    EmitMessage("QueueEnabled", "");
                }
                else if (command == "disablequeue" && requestsEnabled == true &&
                         (Config.LetModsChangeQueueStatus && twitchMessage.Mod == "1" || twitchMessage.Broadcaster == "1"))
                {
                    RequestUI.EnableQueue(false);
                    EmitMessage("QueueDisabled", "");
                }
            }
        }

        private class RequestData
        {
            public RequestData(string query, string requestedBy, DateTime timeOfRequest)
            {
                Query         = query;
                RequestedBy   = requestedBy;
                TimeOfRequest = timeOfRequest;
            }

            public string   Query;
            public string   RequestedBy;
            public DateTime TimeOfRequest;
        }

        private class QueryData
        {
            public QueryData(RequestData req)
            {
                string queryInvariant = req.Query.ToLowerInvariant();

                Artist    = null;
                Mapper    = null;
                MaudicaID = null;
                FullQuery = queryInvariant;

                string modifiedQuery = queryInvariant + "-endQuery";
                // if an ID is used, it should be possible to convert to int, otherwise it's likely a normal song title
                if (queryInvariant.Contains("-id ")  &&  int.TryParse(queryInvariant.Replace("-id", "").Trim(), out _))
                {
                    // match everything from -id to the next occurrence of a relevant tag
                    Match m        = Regex.Match(modifiedQuery, "-id.*?(?=-mapper|-artist|-endQuery)");
                    MaudicaID      = m.Value.Replace("-id", "").Trim();
                    MaudicaID      = MaudicaID.Replace(" ", "");
                }
                else
                {
                    if (queryInvariant.Contains("-artist "))
                    {
                        // match everything from -artist to the next occurrence of -mapper or -endQuery
                        Match m        = Regex.Match(modifiedQuery, "-artist.*?(?=-mapper|-endQuery)");
                        queryInvariant = queryInvariant.Replace(m.Value, ""); // remove artist part from song title
                        Artist         = m.Value.Replace("-artist", "").Trim();
                        Artist         = Artist.Replace(" ", "");
                    }
                    if (queryInvariant.Contains("-mapper "))
                    {
                        // match everything from -mapper to the next occurrence of -artist or -endQuery
                        Match m        = Regex.Match(modifiedQuery, "-mapper.*?(?=-artist|-endQuery)");
                        queryInvariant = queryInvariant.Replace(m.Value, ""); // remove mapper part from song title
                        Mapper         = m.Value.Replace("-mapper", "").Trim();
                        Mapper         = Mapper.Replace(" ", "");
                    }
                    Title = queryInvariant.Trim();
                }
                RequestedBy = req.RequestedBy;
                RequestedAt = req.TimeOfRequest;
            }

            public string   Title { get; private set; }
            public string   Artist { get; private set; }
            public string   Mapper { get; private set; }
            public string   MaudicaID { get; private set; }
            public string   RequestedBy { get; private set; }
            public DateTime RequestedAt { get; private set; }
            public string   FullQuery { get; private set; }
        }

        #region Queue
        internal static void SaveQueue()
        {
            string text = JSON.Dump(requests);
            File.WriteAllText(queuePath, text);
        }

        internal static bool AddRequest(SongList.SongData s, string requestedBy, DateTime requestedAt)
        {
            Request req = new Request();
            req.SongID  = s.songID;
            req.Title   = s.title; // Cheating a bit so we can emit this object in websocket.
            if (!requests.AvailableSongs.Contains(req))
            {
                // comparison uses only the SongID, so we can save
                // some time by only adding the rest now
                req.Title       = s.title;
                req.Artist      = s.artist;
                req.Mapper      = s.author;
                req.RequestedBy = requestedBy;
                req.RequestedAt = requestedAt;
                requests.AvailableSongs.Add(req);
                SaveQueue();

                bool shouldEmitMessage = true;

                foreach(var entry in downloadNameCache)
                {
                    if(s.songID.Contains(entry))
                    {
                        shouldEmitMessage = false;                           
                    }
                }

                if (shouldEmitMessage)
                {
                    EmitMessage("NewSongQueueItem", req, req.Title, req.Artist, req.Mapper);
                }
                else
                {
                    downloadNameCache.Remove(req.SongID);
                }

                return true;
            }
            
            EmitMessage("SongAlreadyInQueue", req, req.Title);
            
            return false;
        }
        
        private static List<string> downloadNameCache = new List<string>();

        internal static bool AddMissing(Song s, string requestedBy, DateTime requestedAt)
        {
            MissingRequest req = new MissingRequest();
            req.SongID         = s.song_id;
            req.Title          = s.title; // Cheating a bit so we can emit this object in websocket.
            if (!requests.MissingSongs.Contains(req))
            {
                // comparison uses only the SongID, so we can save
                // some time by only adding the rest now
                req.Title       = s.title;
                req.Artist      = s.artist;
                req.Mapper      = s.author;
                req.RequestedBy = requestedBy;
                req.RequestedAt = requestedAt;
                req.DownloadURL = s.download_url;
                req.PreviewURL  = s.preview_url;
                requests.MissingSongs.Add(req);
                SaveQueue();

                EmitMessage("NewSongQueueItem", req, req.Title, req.Artist, req.Mapper);                
                downloadNameCache.Add(s.song_id);

                return true;
            }
            EmitMessage("SongAlreadyInQueue", req, req.Title, req.Artist, req.Mapper);
            
            return false;
        }

        internal static void RemoveRequest(string songID)
        {
            Request remove = new Request();
            remove.SongID  = songID;
            RemoveRequest(remove);
        }
        internal static void RemoveRequest(Request req)
        {
            requests.AvailableSongs.Remove(req);
            SaveQueue();
        }

        internal static void RemoveMissing(MissingRequest missing)
        {
            requests.MissingSongs.Remove(missing);
            SaveQueue();
        }

        internal static void ClearMissing()
        {
            requests.MissingSongs.Clear();
            SaveQueue();
        }

        internal static List<Request> GetRequests()
        {
            return requests.AvailableSongs;
        }

        internal static List<MissingRequest> GetMissingSongs()
        {
            return requests.MissingSongs;
        }

        internal static void LoadQueue()
        {
            if (File.Exists(queuePath))
            {
                try
                {
                    string text = File.ReadAllText(queuePath);
                    requests    = JSON.Load(text).Make<RequestQueue>();

                    // get rid of any requests that had their map deleted
                    // if the user deleted a requested map that request
                    // will be missing but that should be enough of a niche
                    // case that we can ignore it
                    List<Request> availableSongs = new List<Request>();
                    for (int i = 0; i < SongList.I.songs.Count; i++)
                    {
                        foreach (Request req in requests.AvailableSongs)
                        {
                            if (req.SongID == SongList.I.songs[i].songID)
                            {
                                availableSongs.Add(req);
                                break;
                            }
                        }
                    }
                    requests.AvailableSongs = availableSongs;
                }
                catch
                {
                    MelonLogger.Msg("Unable to load queue");
                }

                SaveQueue();
            }
        }

        private static void EmitMessage(string eventType, object data, string title = "", string artist = "", string mapper = "")
        {
            if (hasCompatibleWebsocketServer)
            {
                SendWebsocketMessage(eventType, data);
            }

            if (hasCompatibleTwitchConnectorMod)
            {
                string message;

                switch (eventType)
                {
                    case "RemoveSongQueueItem":
                        message = $"'{title}' removed from the queue.";
                        break;
                    case "QueueEnabled":
                        message = $"Request queue enabled.";
                        break;
                    case "QueueDisabled":
                        message = $"Request queue disabled.";
                        break;
                    case "NewSongQueueItem":
                        message = $"'{title}' by {artist} ({mapper}) added to the queue.";
                        break;
                    case "SongAlreadyInQueue":
                        message = $"'{title}' is already in the queue.";
                        break;
                    case "SongNotFound":
                        message = $"'{title}' not found.";
                        break;
                    default:
                        message = eventType;
                        break;
                }

                SendTwitchMessage(message);
            }
        }

        private static void SendWebsocketMessage(string eventType, object data)
        {
            var eventContainer = new EventContainer();
            eventContainer.eventType = eventType;
            eventContainer.data = data;

            AudicaWebsocketServerMain.EmitWebsocketEvent(eventContainer);
        }

        private static void SendTwitchMessage(string message)
        {
            TwitchConnectorMod.TwitchConnectorMod.SendMessage(message);
        }
        #endregion
    }

    [Serializable]
    public class RequestQueue
    {
        public List<MissingRequest> MissingSongs   = new List<MissingRequest>();
        public List<Request>        AvailableSongs = new List<Request>();
    }

    [Serializable]
    public class Request : IEquatable<Request>
    {
        public string   SongID;
        public string   Title;
        public string   Artist;
        public string   Mapper;
        public string   RequestedBy;
        public DateTime RequestedAt;

        public override bool Equals(object obj)
        {
            return Equals(obj as Request);
        }

        public bool Equals(Request other)
        {
            if (other == null)
            {
                return false;
            }
            return SongID == other.SongID;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [Serializable]
    public class MissingRequest : Request
    {
        public string DownloadURL;
        public string PreviewURL;
    }

    internal class SongInfo
    {
        public string SongID;
        public string Title;
        public string Artist;
        public string Mapper;

        public SongInfo()
        { }

        public SongInfo(Request req)
        {
            SongID = req.SongID;
            Title = req.Title;
            Artist = req.Artist;
            Mapper = req.Mapper;
        }
    }
}



