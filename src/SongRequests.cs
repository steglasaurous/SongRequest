using MelonLoader;
using MelonLoader.TinyJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

[assembly: MelonOptionalDependencies("SongBrowser")]

namespace AudicaModding
{
    public class SongRequests : MelonMod
    {
        public static class BuildInfo
        {
            public const string Name = "SongRequest";  // Name of the Mod.  (MUST BE SET)
            public const string Author = "Alternity"; // Author of the Mod.  (Set as null if none)
            public const string Company = null; // Company that made the Mod.  (Set as null if none)
            public const string Version = "1.2.1"; // Version of the Mod.  (MUST BE SET)
            public const string DownloadLink = null; // Download Link for the Mod.  (Set as null if none)
        }

        internal static bool loadComplete             = false;
        internal static bool hasCompatibleSongBrowser = false;
        internal static SongList.SongData selectedSong;

        internal static bool requestsEnabled = true;

        private static List<string> unprocessedRequests = new List<string>();
        private static RequestQueue requests            = new RequestQueue();

        private static Dictionary<string, QueryData> webSearchQueryData = new Dictionary<string, QueryData>();
        private static string                        queuePath          = UnityEngine.Application.dataPath + "/../" + "/UserData/" + "SongRequestQueue.json";

        public override void OnApplicationStart()
        {
            if (MelonHandler.Mods.Any(HasCompatibleSongBrowser))
            {
                InitSongBrowserIntegration();
            }
        }

        public static int GetActiveWebSearchCount()
        {
            return webSearchQueryData.Count;
        }

        private void InitSongBrowserIntegration()
        {
            hasCompatibleSongBrowser = true;
            MelonLogger.Log("Song Browser is installed. Enabling integration");
            RequestUI.Register();

            // make sure queue is processed after song list reload
            // to show requested maps that were missing and just
            // got downloaded
            SongBrowser.RegisterSongListPostProcessing(ProcessQueueAfterRefresh);
        }

        private bool HasCompatibleSongBrowser(MelonMod mod)
        { 
            if (mod.Info.SystemType.Name == nameof(SongBrowser))
            {
                string[] versionInfo = mod.Info.Version.Split('.');
                int major = int.Parse(versionInfo[0]);
                int minor = int.Parse(versionInfo[1]);
                int patch = int.Parse(versionInfo[2]);
                if (major > 2 || (major == 2 && (minor > 4 || minor == 4 && patch >= 1)))
                    return true;
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
                Request req = new Request
                {
                    SongID = currentSong.songID,
                    Title  = currentSong.title,
                    Artist = currentSong.artist,
                    Mapper = currentSong.author
                };

                if (LookForMatch(data, req, ref foundAny, ref foundBetter, ref foundExactMatch))
                {
                    song = currentSong;
                    if (foundExactMatch)
                        break;
                }
            }
            return song;
        }
        private static bool LookForMatch(QueryData query, Request req,
                                         ref bool foundAny, ref bool foundBetter, ref bool foundExact)
        {
            bool hasArtist = req.Artist == null ||
                             query.Artist == null || req.Artist.ToLowerInvariant().Replace(" ", "").Contains(query.Artist);
            bool hasMapper = req.Mapper == null ||
                             query.Mapper == null || req.Mapper.ToLowerInvariant().Replace(" ", "").Contains(query.Mapper);
            bool hasTitle = req.Title.ToLowerInvariant().Contains(query.Title) ||
                             req.SongID.ToLowerInvariant().Contains(query.Title.Replace(" ", ""));

            if ((hasArtist && hasMapper && hasTitle) ||
                (query.Title == "" && query.Artist != null && hasArtist) ||
                (query.Title == "" && query.Mapper != null && hasMapper))
            {
                return LookForMatch(query.Title, req.Title, ref foundAny, ref foundBetter, ref foundExact);
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
            MelonLogger.Log(unprocessedRequests.Count + " in queue.");
            
            if (unprocessedRequests.Count != 0)
            {
                foreach (string str in unprocessedRequests)
                {
                    QueryData         data   = new QueryData(str);
                    SongList.SongData result = SearchSong(data, out bool foundExactMatch);

                    if ((!hasCompatibleSongBrowser || foundExactMatch) && result != null)
                    {
                        // if we have web search we want to make sure we prioritize exact matches
                        // over partial local ones
                        MelonLogger.Log("Result: " + result.songID);
                        if (AddRequest(result))
                            addedAny = true;
                    }
                    else if (hasCompatibleSongBrowser)
                    {
                        StartWebSearch(data);
                    }
                    else
                    {
                        MelonLogger.Log($"Found no match for \"{str}\"");
                    }
                }
                unprocessedRequests.Clear();
            }
            
            if (addedAny && MenuState.GetState() == MenuState.State.SongPage)
                RequestUI.UpdateFilter();
            
            RequestUI.UpdateButtonText();
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
            webSearchQueryData.Add(search, data);
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
                    Request req = new Request
                    {
                        SongID = s.song_id,
                        Title  = s.title,
                        Artist = s.artist,
                        Mapper = s.author
                    };
                    if (LookForMatch(data, req, ref foundAny, ref foundBetter, ref foundExact))
                    {
                        bestMatch = s;
                        if (foundExact)
                            break;
                    }
                }
                if (bestMatch != null)
                {
                    // check if we already have that file downloaded
                    QueryData matchData = new QueryData($"{bestMatch.title} -artist {bestMatch.artist} -mapper {bestMatch.author}");
                    SongList.SongData s = SearchSong(matchData, out bool isExactMatch);
                    if (isExactMatch)
                    {
                        MelonLogger.Log("Result: " + s.songID);
                        if (AddRequest(s))
                            addedLocalMatch = true;
                    }
                    else if (!requests.MissingSongs.ContainsKey(bestMatch.song_id))
                    {
                        MissingRequest request = new MissingRequest();
                        request.SongID      = bestMatch.song_id;
                        request.Title       = bestMatch.title;
                        request.Artist      = bestMatch.artist;
                        request.Mapper      = bestMatch.author;
                        request.DownloadURL = bestMatch.download_url;
                        request.PreviewURL  = bestMatch.preview_url;
                        AddMissing(request.SongID, request);
                        MelonLogger.Log("Result (missing): " + bestMatch.song_id);
                    }
                }
                else
                {
                    MelonLogger.Log($"Found no match for \"{data.FullQuery}\"");
                }
            }
            else
            {
                // check if we have a local match (can happen if
                // this particular map hasn't been uploaded or was taken down)
                SongList.SongData s = SearchSong(data, out bool _);
                if (s != null)
                {
                    MelonLogger.Log("Result: " + s.songID);
                    if (AddRequest(s))
                        addedLocalMatch = true;
                }
                else
                {
                    MelonLogger.Log($"Found no match for \"{data.FullQuery}\"");
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
            foreach (string s in requests.MissingSongs.Keys)
            {
                MissingRequest addedInfo = GetMissing(s);
                unprocessedRequests.Add($"{addedInfo.Title} -artist {addedInfo.Artist} -mapper {addedInfo.Mapper}");
            }
            ClearMissing();

            ProcessQueue();
        }

        public static void ProcessRemoval(string arguments)
        {
            QueryData query = new QueryData(arguments);

            bool foundAny        = false;
            bool foundBetter     = false;
            bool foundExactMatch = false;
            int  matchIdx        = -1;

            // check available songs
            for (int i = 0; i < requests.AvailableSongs.Count; i++)
            {
                AvailableRequest req = requests.AvailableSongs[i];

                if (LookForMatch(query, req, ref foundAny, ref foundBetter, ref foundExactMatch))
                {
                    matchIdx = i;
                    if (foundExactMatch)
                        break;
                }
            }
            if (matchIdx != -1)
            {
                RemoveRequest(requests.AvailableSongs[matchIdx].SongID);
                MelonLogger.Log("Removed \"" + arguments + "\" from available requests");
                if (MenuState.GetState() == MenuState.State.SongPage)
                {
                    RequestUI.UpdateFilter();
                }
            }
            else
            {
                List<string> keys = new List<string>(requests.MissingSongs.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    MissingRequest req = requests.MissingSongs[keys[i]];

                    if (LookForMatch(query, req, ref foundAny, ref foundBetter, ref foundExactMatch))
                    {
                        matchIdx = i;
                        if (foundExactMatch)
                            break;
                    }
                }
                if (matchIdx != -1)
                {
                    RemoveMissing(keys[matchIdx]);
                    MelonLogger.Log("Removed \"" + arguments + "\" from missing requests");
                }
            }
            if (matchIdx != -1 && MenuState.GetState() == MenuState.State.SongPage)
            {
                RequestUI.UpdateButtonText();
            }
        }

        public static void ParseCommand(ParsedTwitchMessage twitchMessage)
        {
            string msg = twitchMessage.Message;
            if (msg.Length > 2 && msg.Substring(0, 1) == "!") // length has to be at least 2: ! and at least one command letter
            {
                string command = msg.Replace("!", "").Split(" ".ToCharArray())[0];
                string arguments = msg.Replace("!" + command + " ", "");

                if (command == "asr" && requestsEnabled)
                {
                    MelonLogger.Log("!asr requested with query \"" + arguments + "\"");

                    unprocessedRequests.Add(arguments);

                    if (loadComplete)
                    {
                        ProcessQueue();
                    }
                }
                else if ((command == "remove" || command == "yeet") && twitchMessage.Mod == "1")
                {
                    MelonLogger.Log("!remove requested with query \"" + arguments + "\"");
                    ProcessRemoval(arguments);
                }
            }
        }

        private class QueryData
        {
            public QueryData(string query)
            {
                string queryInvariant = query.ToLowerInvariant();

                Artist    = null;
                Mapper    = null;
                FullQuery = queryInvariant;

                string modifiedQuery = queryInvariant + "-endQuery";
                if (queryInvariant.Contains("-artist"))
                {
                    // match everything from -artist to the next occurrence of -mapper or -endQuery
                    Match m        = Regex.Match(modifiedQuery, "-artist.*?(?=-mapper|-endQuery)");
                    queryInvariant = queryInvariant.Replace(m.Value, ""); // remove artist part from song title
                    Artist         = m.Value.Replace("-artist", "").Trim();
                    Artist         = Artist.Replace(" ", "");
                }
                if (queryInvariant.Contains("-mapper"))
                {
                    // match everything from -mapper to the next occurrence of -artist or -endQuery
                    Match m        = Regex.Match(modifiedQuery, "-mapper.*?(?=-artist|-endQuery)");
                    queryInvariant = queryInvariant.Replace(m.Value, ""); // remove mapper part from song title
                    Mapper         = m.Value.Replace("-mapper", "").Trim();
                    Mapper         = Mapper.Replace(" ", "");
                }
                Title = queryInvariant.Trim();
            }

            public string Title { get; private set; }
            public string Artist { get; private set; }
            public string Mapper { get; private set; }
            public string FullQuery { get; private set; }
        }

        #region Queue
        internal static void SaveQueue()
        {
            string text = JSON.Dump(requests);
            File.WriteAllText(queuePath, text);
        }

        internal static bool AddRequest(SongList.SongData s)
        {
            AvailableRequest req = new AvailableRequest();
            req.SongID           = s.songID;
            if (!requests.AvailableSongs.Contains(req))
            {
                // comparison uses only the SongID, so we can save 
                // some time by only adding the rest now
                req.Title  = s.title;
                req.Artist = s.artist;
                req.Mapper = s.author;
                AddRequest(req);
                return true;
            }
            return false;
        }

        internal static void AddRequest(AvailableRequest request)
        {
            requests.AvailableSongs.Add(request);
            SaveQueue();
        }

        internal static void AddMissing(string songID, MissingRequest song)
        {
            requests.MissingSongs.Add(songID, song);
            SaveQueue();
        }

        internal static void RemoveRequest(string songID)
        {
            AvailableRequest remove = new AvailableRequest();
            remove.SongID           = songID;
            requests.AvailableSongs.Remove(remove);
            SaveQueue();
        }

        internal static void RemoveMissing(string songID)
        {
            requests.MissingSongs.Remove(songID);
            SaveQueue();
        }

        internal static void ClearMissing()
        {
            requests.MissingSongs.Clear();
            SaveQueue();
        }

        internal static List<AvailableRequest> GetRequests()
        {
            return requests.AvailableSongs;
        }

        internal static List<string> GetMissingSongs()
        {
            return new List<string>(requests.MissingSongs.Keys);
        }

        internal static MissingRequest GetMissing(string songID)
        {
            return requests.MissingSongs[songID];
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
                    List<AvailableRequest> availableSongs = new List<AvailableRequest>();
                    for (int i = 0; i < SongList.I.songs.Count; i++)
                    {
                        AvailableRequest newReq = new AvailableRequest();
                        newReq.SongID           = SongList.I.songs[i].songID;
                        if (requests.AvailableSongs.Contains(newReq))
                        {
                            // comparison uses only the SongID, so we can save 
                            // some time by only adding the rest now
                            newReq.Title  = SongList.I.songs[i].title;
                            newReq.Artist = SongList.I.songs[i].artist;
                            newReq.Mapper = SongList.I.songs[i].author;
                            availableSongs.Add(newReq);
                        }
                    }
                    requests.AvailableSongs = availableSongs;
                }
                catch
                {
                    MelonLogger.Log("Unable to load queue");
                }

                SaveQueue();
            }
        }
        #endregion
    }

    [Serializable]
    public class RequestQueue
    {
        public Dictionary<string, MissingRequest> MissingSongs   = new Dictionary<string, MissingRequest>();
        public List<AvailableRequest>             AvailableSongs = new List<AvailableRequest>();
    }

    public class Request
    {
        public string SongID;
        public string Title;
        public string Artist;
        public string Mapper;
    }

    [Serializable]
    public class MissingRequest : Request
    {
        public string DownloadURL;
        public string PreviewURL;
    }

    [Serializable]
    public class AvailableRequest : Request, IEquatable<AvailableRequest>
    {

        public override bool Equals(object obj)
        {
            return Equals(obj as AvailableRequest);
        }

        public bool Equals(AvailableRequest other)
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
}



