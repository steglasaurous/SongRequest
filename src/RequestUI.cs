using TMPro;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace AudicaModding
{
    internal static class RequestUI
    {
        public static bool requestFilterActive = false;

        private static bool additionalGUIActive = false;

        private static GameObject  filterSongRequestsButton       = null;
        private static TextMeshPro filterButtonText               = null;
        private static GameObject  requestButtonSelectedIndicator = null;
        private static GameObject  skipSongRequestsButton         = null;
        private static GameObject  downloadMissingButton          = null;
        private static TextMeshPro downloadButtonText             = null;
        private static GunButton   downloadGunButton              = null;
        private static GameObject  queueOnOffButton               = null;
        private static TextMeshPro queueOnOffButtonText           = null;

        private static Vector3     filterSongRequestsButtonPos   = new Vector3(0.0f, 10.5f, 0.0f);
        private static Vector3     filterSongRequestsButtonScale = new Vector3(2.8f, 2.8f, 2.8f);
                                   
        private static Vector3     skipButtonPos                 = new Vector3(4f, 15.1f, 0.0f);
        private static Vector3     skipButtonScale               = new Vector3(1.0f, 1.0f, 1.0f);

        private static Vector3     downloadButtonPos             = new Vector3(0.0f, 15.1f, 0.0f);
        private static Vector3     downloadButtonScale           = new Vector3(1.0f, 1.0f, 1.0f);

        private static Vector3     queueOnOffButtonPos           = new Vector3(0.0f, 17.1f, 0.0f);
        private static Vector3     queueOnOffButtonScale         = new Vector3(1.0f, 1.0f, 1.0f);

        private static SongSelect       songSelect       = null;
        private static SongListControls songListControls = null;

        private static System.Func<object> getFilter = null; // for use with song browser integration, actually of FilterPanel.Filter

        // if compatible version of song browser is available, use song browser's filter panel
        public static void Register()
        {
            getFilter = FilterPanel.RegisterFilter("requests", true, "Song Requests",
                                                   ShowAdditionalGUI, HideAdditonalGUI,
                                                   ApplyFilter);
        }

        public static void Initialize()
        {
            if (songListControls == null)
            {
                songSelect       = GameObject.FindObjectOfType<SongSelect>();
                songListControls = GameObject.FindObjectOfType<SongListControls>();

                if (!SongRequests.hasCompatibleSongBrowser) // song browser integration does this automatically
                {
                    CreateSongRequestFilterButton();

                    // move that button down, since the download button doesn't exist
                    skipButtonPos   = downloadButtonPos;
                    skipButtonScale = downloadButtonScale;
                }
                CreateQueueOnOffButton();
                CreateSongRequestSkipButton();
                CreateDownloadMissingButton();
            }
        }

        public static void DisableFilter()
        {
            requestFilterActive = false;
            requestButtonSelectedIndicator.SetActive(false);
            HideAdditonalGUI();
        }

        public static void UpdateButtonText(bool processing = false)
        {
            TextMeshPro buttonText = null;
            if (SongRequests.hasCompatibleSongBrowser)
            {
                buttonText = GetSongBrowserFilterButtonText();
            }
            else
            {
                if (filterSongRequestsButton == null)
                    return;
                if (filterButtonText == null)
                    filterButtonText = filterSongRequestsButton.GetComponentInChildren<TextMeshPro>();

                buttonText = filterButtonText;
            }

            if (buttonText == null)
                return;

            List<MissingRequest> missingSongs = SongRequests.GetMissingSongs();

            if (SongRequests.GetRequests().Count == 0 && missingSongs.Count == 0)
            {
                if (buttonText.text.Contains("=green>"))
                {
                    buttonText.text = buttonText.text.Replace("=green>", "=red>");
                }
                else if (!buttonText.text.Contains("=red>"))
                {
                    buttonText.text = "<color=red>" + buttonText.text + "</color>";
                }
            }
            else
            {
                if (buttonText.text.Contains("=red>"))
                {
                    buttonText.text = buttonText.text.Replace("=red>", "=green>");
                }
                else if (!buttonText.text.Contains("=green>"))
                {
                    buttonText.text = "<color=green>" + buttonText.text + "</color>";
                }
            }

            // update 
            if (SongRequests.hasCompatibleSongBrowser && downloadMissingButton != null)
            {
                if (SongRequests.GetActiveWebSearchCount() > 0)
                {
                    downloadButtonText.text = "Processing...";
                    downloadGunButton.SetInteractable(false);
                }
                else if (missingSongs.Count > 0)
                {
                    downloadButtonText.text = $"<color=green>Download {missingSongs.Count} missing song(s)</color>";
                    downloadGunButton.SetInteractable(true);
                }
                else
                {
                    downloadButtonText.text = "No songs missing";
                    downloadGunButton.SetInteractable(false);
                }
            }
        }
        private static TextMeshPro GetSongBrowserFilterButtonText()
        {
            return ((FilterPanel.Filter)getFilter())?.ButtonText;
        }

        public static void ShowAdditionalGUI()
        {
            additionalGUIActive = true;
            queueOnOffButton?.SetActive(true);
            skipSongRequestsButton?.SetActive(true);
            downloadMissingButton?.SetActive(true);
        }
        public static void HideAdditonalGUI()
        {
            additionalGUIActive = false;
            queueOnOffButton?.SetActive(false);
            skipSongRequestsButton?.SetActive(false);
            downloadMissingButton?.SetActive(false);
        }

        public static void UpdateFilter()
        {
            if ((SongRequests.hasCompatibleSongBrowser && IsSongBrowserFilterActive()) || requestFilterActive)
                songSelect?.ShowSongList();
        }
        private static bool IsSongBrowserFilterActive()
        {
            return ((FilterPanel.Filter)getFilter()).IsActive;
        }

        public static void EnableQueue(bool enable)
        {
            if (enable)
            {
                SongRequests.requestsEnabled = true;
                if (queueOnOffButtonText != null)
                    queueOnOffButtonText.text = GetQueueOnOffText();
            }
            else
            {
                SongRequests.requestsEnabled = false;
                if (queueOnOffButtonText != null)
                    queueOnOffButtonText.text = GetQueueOnOffText();
            }
        }

        private static GameObject CreateButton(GameObject buttonPrefab, string label, System.Action onHit, Vector3 position, Vector3 scale)
        {
            GameObject buttonObject = Object.Instantiate(buttonPrefab, buttonPrefab.transform.parent);
            buttonObject.transform.localPosition    = position;
            buttonObject.transform.localScale       = scale;
            buttonObject.transform.localEulerAngles = new Vector3(0.0f, 0.0f, 0.0f);

            Object.Destroy(buttonObject.GetComponentInChildren<Localizer>());
            TextMeshPro buttonText = buttonObject.GetComponentInChildren<TextMeshPro>();
            buttonText.text = label;
            GunButton button = buttonObject.GetComponentInChildren<GunButton>();
            button.destroyOnShot = false;
            button.disableOnShot = false;
            button.SetSelected(false);
            button.onHitEvent = new UnityEvent();
            button.onHitEvent.AddListener(onHit);

            return buttonObject.gameObject;
        }

        private static void CreateSongRequestFilterButton()
        {
            if (filterSongRequestsButton != null)
            {
                filterSongRequestsButton.SetActive(true);
                return;
            }

            GameObject filterMainButton = GameObject.Find("menu/ShellPage_Song/page/ShellPanel_Left/FilterExtras");
            if (filterMainButton == null)
                return;

            filterSongRequestsButton = CreateButton(filterMainButton, "Song Requests", OnFilterSongRequestsShot, 
                                                    filterSongRequestsButtonPos, filterSongRequestsButtonScale);

            requestButtonSelectedIndicator = filterSongRequestsButton.transform.GetChild(3).gameObject;
            requestButtonSelectedIndicator.SetActive(requestFilterActive);

            filterMainButton.GetComponentInChildren<GunButton>().onHitEvent.AddListener(new System.Action(() =>
            {
                DisableFilter();
                songSelect.ShowSongList();
            }));

            UpdateButtonText();
        }

        private static void CreateQueueOnOffButton()
        {
            if (queueOnOffButton != null)
            {
                queueOnOffButton.SetActive(additionalGUIActive);
                return;
            }

            GameObject backButton = GameObject.Find("menu/ShellPage_Song/page/backParent/back");
            if (backButton == null)
                return;

            queueOnOffButton = CreateButton(backButton, GetQueueOnOffText(), OnQueueOnOffShot, 
                                            queueOnOffButtonPos, queueOnOffButtonScale);

            queueOnOffButton.SetActive(additionalGUIActive);

            queueOnOffButtonText = queueOnOffButton.GetComponentInChildren<TextMeshPro>();
        }

        private static void CreateSongRequestSkipButton()
        {
            if (skipSongRequestsButton != null)
            {
                skipSongRequestsButton.SetActive(additionalGUIActive);
                return;
            }

            GameObject backButton = GameObject.Find("menu/ShellPage_Song/page/backParent/back");
            if (backButton == null)
                return;

            skipSongRequestsButton = CreateButton(backButton, "Skip Next", OnSkipSongRequestShot, 
                                                  skipButtonPos, skipButtonScale);

            skipSongRequestsButton.SetActive(additionalGUIActive);
        }

        private static void CreateDownloadMissingButton()
        {
            if (!SongRequests.hasCompatibleSongBrowser)
                return;

            if (downloadMissingButton != null)
            {
                downloadMissingButton.SetActive(additionalGUIActive);
                return;
            }

            GameObject backButton = GameObject.Find("menu/ShellPage_Song/page/backParent/back");
            if (backButton == null)
                return;

            downloadMissingButton = CreateButton(backButton, "Download missing", OnDownloadMissingShot,
                                                 downloadButtonPos, downloadButtonScale);

            downloadMissingButton.SetActive(additionalGUIActive);

            downloadGunButton  = downloadMissingButton.GetComponentInChildren<GunButton>();
            downloadButtonText = downloadMissingButton.GetComponentInChildren<TextMeshPro>();

            UpdateButtonText();
        }

        private static bool ApplyFilter(Il2CppSystem.Collections.Generic.List<string> result)
        {
            result.Clear();

            foreach (Request req in SongRequests.GetRequests())
            {
                result.Add(req.SongID);
            }
            return true;
        }

        private static void OnFilterSongRequestsShot()
        {
            songListControls.FilterExtras(); // this seems to fix duplicated songs;
            if (!requestFilterActive)
            {
                requestFilterActive = true;
                requestButtonSelectedIndicator.SetActive(true);
                ShowAdditionalGUI();
            }
            else
            {
                DisableFilter();
            }
            songSelect.ShowSongList();
        }

        private static void OnQueueOnOffShot()
        {
            EnableQueue(!SongRequests.requestsEnabled);
        }

        private static string GetQueueOnOffText()
        {
            return SongRequests.requestsEnabled ? "<color=green>Requests Enabled</color>" : "<color=red>Requests Disabled</color>";
        }

        private static void OnDownloadMissingShot()
        {
            MissingSongsUI.lookingAtMissingSongs = true;
            MenuState.I.GoToSettingsPage();
            // moves to search page next via Hooks.PatchShowOptionsPage.Postfix()
        }

        private static void OnSkipSongRequestShot()
        {
            if (SongRequests.GetRequests().Count > 0 && songSelect != null && songSelect.songSelectItems != null && songSelect.songSelectItems.mItems != null)
            {
                string id = songSelect.songSelectItems.mItems[0].mSongData.songID;
                SongRequests.RemoveRequest(id);
                UpdateButtonText();
                songSelect.ShowSongList();
            }
        }
    }
}



