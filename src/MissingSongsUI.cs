using System.Collections;
using System;
using TMPro;
using UnityEngine;
using MelonLoader;
using UnityEngine.Events;
using System.Collections.Generic;

namespace AudicaModding
{
	internal static class MissingSongsUI
	{
		public static bool lookingAtMissingSongs = false;
		public static bool needsSongListRefresh  = false;

		private static OptionsMenu songItemMenu;
		private static GunButton   backButton;
		private static GameObject  downloadAllButton;
		private static GunButton   downloadAllGunButton;

		private static List<MissingRequest> missingSongs = null;
		private static int downloadCount = 0;

		public static void SetMenu(OptionsMenu optionsMenu)
		{
			songItemMenu = optionsMenu;
		}

		public static void GoToMissingSongsPage()
		{
			needsSongListRefresh = false;

			songItemMenu.ShowPage(OptionsMenu.Page.Customization);

			if (backButton == null)
            {
				var button = GameObject.Find("menu/ShellPage_Settings/page/backParent/back");
				backButton = button.GetComponentInChildren<GunButton>();

				// set up "download all" button
				downloadAllButton						     = UnityEngine.Object.Instantiate(button, button.transform.parent);
				downloadAllButton.transform.localPosition    = new Vector3(-0.8f, 2.0f, 0.0f);
				downloadAllButton.transform.localScale       = new Vector3(1.5f, 1.5f, 1.5f);
				downloadAllButton.transform.localEulerAngles = new Vector3(0.0f, 0.0f, 0.0f);

				UnityEngine.Object.Destroy(downloadAllButton.GetComponentInChildren<Localizer>());
				TextMeshPro buttonText = downloadAllButton.GetComponentInChildren<TextMeshPro>();
				buttonText.text        = "Download all";

				Action    onHit                       = new Action(() => { OnDownloadAll(); });
				downloadAllGunButton                  = downloadAllButton.GetComponentInChildren<GunButton>();
				downloadAllGunButton.destroyOnShot    = false;
				downloadAllGunButton.disableOnShot    = false;
				downloadAllGunButton.onHitEvent       = new UnityEvent();
				downloadAllGunButton.onHitEvent.AddListener(onHit);
				downloadAllGunButton.SetSelected(false);
			}
			else
            {
				downloadAllButton.SetActive(true);
            }

			missingSongs = new List<MissingRequest>(SongRequests.GetMissingSongs());
			SetupList();
			AddSongItems(songItemMenu);
		}

		public static void Cancel()
        {
			lookingAtMissingSongs = false;
			downloadAllButton.SetActive(false);

			if (needsSongListRefresh)
			{
				LeaveAndReload();
			}
			else
			{
				MenuState.I.GoToSongPage();
				RequestUI.UpdateButtonText();
			}
		}

		public static void ResetScrollPosition()
		{
			songItemMenu?.scrollable.SnapTo(0);
		}

		private static void SetupList()
		{
			songItemMenu.ShowPage(OptionsMenu.Page.Customization);
			CleanUpPage(songItemMenu);
		}

		public static void AddSongItems(OptionsMenu optionsMenu)
		{
			CleanUpPage(optionsMenu);
			songItemMenu.screenTitle.text = "Missing " + missingSongs.Count + " songs";

			foreach (MissingRequest req in missingSongs)
			{
				CreateSongItem(req, optionsMenu);
			}

			if (missingSongs.Count == 0)
			{
				downloadAllGunButton.SetInteractable(false);
			}
			else
            {
				downloadAllGunButton.SetInteractable(true);
            }
		}

		private static void CreateSongItem(MissingRequest song, OptionsMenu optionsMenu)
		{
			var row = new Il2CppSystem.Collections.Generic.List<GameObject>();

			var textBlock   = optionsMenu.AddTextBlock(0, song.Title + " - " + song.Artist + " (mapped by " + song.Mapper + ")");
			var TMP         = textBlock.transform.GetChild(0).GetComponent<TextMeshPro>();
			TMP.fontSizeMax = 32;
			TMP.fontSizeMin = 8;
			optionsMenu.scrollable.AddRow(textBlock.gameObject);

			// Skip button
			bool   destroyOnShot = true;
			Action onHit         = new Action(() => {
				missingSongs.Remove(song); // remove from local copy
				SongRequests.RemoveMissing(song); // remove from main list
				AddSongItems(optionsMenu); // refresh list
			});

			var skipButton = optionsMenu.AddButton(1,
				"Skip",
				onHit,
				null,
				null);
			skipButton.button.destroyOnShot   = destroyOnShot;
			skipButton.button.doMeshExplosion = destroyOnShot;

			// Download button
			Action onHit2 = new Action(() => {
				StartDownload(song.SongID, song.DownloadURL, TMP);
			});

			var downloadButton = optionsMenu.AddButton(0,
				"Download",
				onHit2,
				null,
				null);
			downloadButton.button.destroyOnShot   = destroyOnShot;
			downloadButton.button.doMeshExplosion = destroyOnShot;

			// Preview button
			var previewButton = optionsMenu.AddButton(0,
				"Preview",
				new Action(() => { MelonCoroutines.Start(SongDownloader.StreamPreviewSong(song.PreviewURL)); }),
				null,
				null);

			optionsMenu.scrollable.AddRow(previewButton.gameObject);
			row.Add(downloadButton.gameObject);
			row.Add(skipButton.gameObject);

			optionsMenu.scrollable.AddRow(row);
		}

		private static void StartDownload(string songID, string downloadURL, TextMeshPro tmp)
		{
			backButton.SetInteractable(false);

			downloadCount++;
			MissingRequest req = new MissingRequest() { SongID = songID };
			missingSongs.Remove(req); // remove from local list so we don't queue it up again if Download All is used
			AddSongItems(songItemMenu); // refresh list
			MelonCoroutines.Start(SongDownloader.DownloadSong(songID, downloadURL, OnDownloadDone));
		}

		private static void OnDownloadDone(string songID, bool success)
        {
			downloadCount--;
			if (success)
			{
				needsSongListRefresh = true;
			}
			else
			{
				KataConfig.I.CreateDebugText($"{songID} is unavailable", new Vector3(0f, -1f, 5f), 5f, null, false, 0.2f);
				MissingRequest req = new MissingRequest() { SongID = songID };
				missingSongs.Remove(req); // remove from local copy
				SongRequests.RemoveMissing(req); // remove from main list
			}
			backButton.SetInteractable(true);
		}

		private static void OnDownloadAll()
		{
			if (missingSongs.Count == 0)
            {
				return;
            }

			downloadAllButton.SetActive(false);
			backButton.SetInteractable(false);

			CleanUpPage(songItemMenu); 
			
			var textBlock = songItemMenu.AddTextBlock(0, "Downloading...");
			var TMP       = textBlock.transform.GetChild(0).GetComponent<TextMeshPro>();
			TMP.fontSizeMax = 32;
			TMP.fontSizeMin = 17;
			songItemMenu.scrollable.AddRow(textBlock.gameObject);

			lookingAtMissingSongs = false;
			needsSongListRefresh  = false;
			KataConfig.I.CreateDebugText("Downloading missing songs...", new Vector3(0f, -1f, 5f), 5f, null, false, 0.2f);
			MelonCoroutines.Start(DownloadAll());
		}

		private static IEnumerator DownloadAll()
		{
			yield return new WaitForSeconds(0.5f);
			List<MissingRequest> missing = new List<MissingRequest>(missingSongs); // don't risk having missingSongs modified mid-loop
			foreach (MissingRequest req in missing)
			{
				downloadCount++;
				MelonCoroutines.Start(SongDownloader.DownloadSong(req.SongID, req.DownloadURL, OnDownloadAllComplete));
				yield return null;
			}
		}

		private static void OnDownloadAllComplete(string songID, bool success)
        {
			downloadCount--;
			if (!success)
			{
				KataConfig.I.CreateDebugText($"{songID} is unavailable", new Vector3(0f, -1f, 5f), 5f, null, false, 0.2f);
				MissingRequest req = new MissingRequest() { SongID = songID };
				missingSongs.Remove(req); // remove from local copy
				SongRequests.RemoveMissing(req); // remove from main list
			}

			if (downloadCount == 0)
			{
				LeaveAndReload();
			}
		}

		private static void LeaveAndReload()
		{
			// make sure people can't shoot this
			GunButton soloButton = GameObject.Find("menu/ShellPage_Main/page/ShellPanel_Center/Solo/Button").GetComponent<GunButton>();
			soloButton.SetInteractable(false);

			// re-enable buttons
			backButton.SetInteractable(true);
			downloadAllGunButton.SetInteractable(true);

			MenuState.I.GoToMainPage();
			SongBrowser.ReloadSongList(false);
			soloButton.SetInteractable(true);
		}

		private static void CleanUpPage(OptionsMenu optionsMenu)
		{
			Transform optionsTransform = optionsMenu.transform;
			for (int i = 0; i < optionsTransform.childCount; i++)
			{
				Transform child = optionsTransform.GetChild(i);
				if (child.gameObject.name.Contains("(Clone)"))
				{
					GameObject.Destroy(child.gameObject);
				}
			}
			optionsMenu.mRows.Clear();
			optionsMenu.scrollable.ClearRows();
			optionsMenu.scrollable.mRows.Clear();
		}
	}
}