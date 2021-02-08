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
            if (msg.Bits != "")
            {
                return 0;
            }
            else
            {
                int totalBits = 0;
                foreach (string str in msg.Bits.Split(",".ToCharArray()))
                {
                    totalBits += System.Convert.ToInt32(str);
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
                bool hasArtist = currentSong.artist == null || 
                                 data.Artist == null || currentSong.artist.ToLowerInvariant().Replace(" ", "").Contains(data.Artist);
                bool hasMapper = currentSong.author == null || 
                                 data.Mapper == null || currentSong.author.ToLowerInvariant().Replace(" ", "").Contains(data.Mapper);
                bool hasTitle  = currentSong.title.ToLowerInvariant().Contains(data.Title) ||
                                 currentSong.songID.ToLowerInvariant().Contains(data.Title.Replace(" ", ""));

                if ((hasArtist && hasMapper && hasTitle) ||
                    (data.Title == "" && data.Artist != null && hasArtist) ||
                    (data.Title == "" && data.Mapper != null && hasMapper))
                {
                    if (LookForMatch(data.Title, currentSong.title, ref foundAny, ref foundBetter, ref foundExactMatch))
                    {
                        song = currentSong;
                        if (foundExactMatch)
                            break;
                    }
                }
            }
            return song;
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
                        if (!requests.SongIDs.Contains(result.songID))
                        {
                            AddRequest(result.songID);
                            addedAny = true;
                        }
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
                    bool hasArtist = s.artist == null || data.Artist == null || s.artist.ToLowerInvariant().Replace(" ", "").Contains(data.Artist);
                    bool hasMapper = s.author == null || data.Mapper == null || s.author.ToLowerInvariant().Replace(" ", "").Contains(data.Mapper);
                    bool hasTitle  = s.title.ToLowerInvariant().Contains(data.Title) ||
                                     s.song_id.ToLowerInvariant().Contains(data.Title.Replace(" ", ""));

                    if ((hasArtist && hasMapper && hasTitle) ||
                        (data.Title == "" && data.Artist != null && hasArtist) ||
                        (data.Title == "" && data.Mapper != null && hasMapper))
                    {
                        if (LookForMatch(data.Title, s.title, ref foundAny, ref foundBetter, ref foundExact))
                        {
                            bestMatch = s;
                            if (foundExact)
                                break;
                        }
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
                        if (!requests.SongIDs.Contains(s.songID))
                        {
                            AddRequest(s.songID);
                            addedLocalMatch = true;
                        }
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
                    if (!requests.SongIDs.Contains(s.songID))
                    {
                        AddRequest(s.songID);
                        addedLocalMatch = true;
                    }
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

        public static void ParseCommand(string msg)
        {
            if (msg.Substring(0, 1) == "!")
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

        internal static void AddRequest(string songID)
        {
            requests.SongIDs.Add(songID);
            SaveQueue();
        }

        internal static void AddMissing(string songID, MissingRequest song)
        {
            requests.MissingSongs.Add(songID, song);
            SaveQueue();
        }

        internal static void RemoveRequest(string songID)
        {
            requests.SongIDs.Remove(songID);
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

        internal static List<string> GetRequests()
        {
            return requests.SongIDs;
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
                string text = File.ReadAllText(queuePath);
                requests    = JSON.Load(text).Make<RequestQueue>();

                // get rid of any requests that had their map deleted
                // if the user deleted a requested map that request
                // will be missing but that should be enough of a niche
                // case that we can ignore it
                List<string> availableSongs = new List<string>();
                for (int i = 0; i < SongList.I.songs.Count; i++)
                {
                    string songID = SongList.I.songs[i].songID;
                    if (requests.SongIDs.Contains(songID))
                    {
                        availableSongs.Add(songID);
                    }
                }
                requests.SongIDs = availableSongs;

                SaveQueue();
            }
        }
        #endregion
    }

    [Serializable]
    public class RequestQueue
    {
        public Dictionary<string, MissingRequest> MissingSongs = new Dictionary<string, MissingRequest>();
        public List<string> SongIDs = new List<string>();
    }

    [Serializable]
    public class MissingRequest
    {
        public string SongID;
        public string Title;
        public string Artist;
        public string Mapper;
        public string DownloadURL;
        public string PreviewURL;
    }
}



