﻿using HarmonyLib;
using System;
using TwitchChatter;

namespace AudicaModding
{
    internal static class Hooks
    {
        private static int buttonCount = 0;

        [HarmonyPatch(typeof(TwitchChatStream), "write_chat_msg", new Type[] { typeof(string) })]
        private static class PatchWriteChatMsg
        {
            private static void Prefix(string msg)
            {
                if (msg.Length > 1)
                {
                    if (msg.Substring(0, 1) == "@")
                    {
                        if (msg.Contains("tmi.twitch.tv PRIVMSG "))
                        {
                            SongRequests.ParseCommand(new ParsedTwitchMessage(msg));
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(StartupLoader), "SetState", new Type[] { typeof(StartupLoader.State) })]
        private static class StartupLoaderSetStatePatch
        {
            private static void Postfix(StartupLoader __instance, ref StartupLoader.State newState)
            {
                if (newState == StartupLoader.State.Complete)
                {
                    SongRequests.loadComplete = true;
                    SongRequests.LoadQueue();
                    SongRequests.ProcessQueue();
                }
            }
        }

        [HarmonyPatch(typeof(MenuState), "SetState", new Type[] { typeof(MenuState.State) })]
        private static class MenuStateSetStatePatch
        {
            private static void Postfix(MenuState __instance, ref MenuState.State state)
            {
                if (state == MenuState.State.SongPage)
                {
                    RequestUI.UpdateButtonText();
                }
            }
        }

        [HarmonyPatch(typeof(SongListControls), "FilterAll")]
        private static class PatchFilterAll
        {
            private static void Prefix(SongListControls __instance)
            {
                if (!SongRequests.hasCompatibleSongBrowser)
                    RequestUI.DisableFilter();
            }
        }

        [HarmonyPatch(typeof(SongListControls), "FilterMain")]
        private static class PatchFilterMain
        {
            private static void Prefix(SongListControls __instance)
            {
                if (!SongRequests.hasCompatibleSongBrowser)
                    RequestUI.DisableFilter();
            }
        }

        [HarmonyPatch(typeof(SongSelect), "GetSongIDs", new Type[] {typeof(bool) } )]
        private static class PatchGetSongIDs
        {
            private static void Postfix(SongSelect __instance, ref bool extras, ref Il2CppSystem.Collections.Generic.List<string> __result)
            {
                if (!SongRequests.hasCompatibleSongBrowser && RequestUI.requestFilterActive)
                {
                    extras = true;
                    __result.Clear();
                    __instance.songSelectHeaderItems.mItems[0].titleLabel.text = "Song Requests";

                    foreach (Request req in SongRequests.GetRequests())
                    {
                        __result.Add(req.SongID);
                    }
                    __instance.scroller.SnapTo(0);
                }
            }
        }

        [HarmonyPatch(typeof(SongSelect), "OnEnable", new Type[0])]
        private static class AdjustSongSelect
        {
            private static void Postfix(SongSelect __instance)
            {
                RequestUI.Initialize();
            }
        }
        
        // clean up requested songs once they've been played or failed
        [HarmonyPatch(typeof(InGameUI), "SetState", new Type[] { typeof(InGameUI.State), typeof(bool) })]
        private static class PatchSetInGameUIState
        {
            private static void Postfix(InGameUI __instance, InGameUI.State state, bool instant)
            {
                if (state == InGameUI.State.FailedPage || state == InGameUI.State.ResultsPage)
                {
                    if (Config.AutomaticallyRemoveSongs)
                    {
                        Request match = null;
                        foreach (Request req in SongRequests.GetRequests())
                        {
                            if (req.SongID == SongDataHolder.I.songData.songID)
                            {
                                match = req;
                                break;
                            }
                        }
                        if (match != null)
                        {
                            SongRequests.RemoveRequest(match);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(OptionsMenu), "ShowPage", new Type[] { typeof(OptionsMenu.Page) })]
        private static class PatchShowOptionsPage
        {
            private static void Prefix(OptionsMenu __instance, OptionsMenu.Page page)
            {
                buttonCount = 0;
            }
            private static void Postfix(InGameUI __instance, OptionsMenu.Page page)
            {
                if (page == OptionsMenu.Page.Main && MissingSongsUI.lookingAtMissingSongs)
                {
                    MissingSongsUI.GoToMissingSongsPage();
                }
            }
        }

        [HarmonyPatch(typeof(OptionsMenu), "BackOut", new Type[0])]
        private static class Backout
        {
            private static bool Prefix(OptionsMenu __instance)
            {
                // should always be on the missing songs page when this happens
                if (MissingSongsUI.lookingAtMissingSongs)
                {
                    MissingSongsUI.Cancel();
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(OptionsMenu), "AddButton", new Type[] { typeof(int), typeof(string), typeof(OptionsMenuButton.SelectedActionDelegate), typeof(OptionsMenuButton.IsCheckedDelegate), typeof(string), typeof(OptionsMenuButton), })]
        private static class AddButtonButton
        {
            private static void Postfix(OptionsMenu __instance, int col, string label, OptionsMenuButton.SelectedActionDelegate onSelected, OptionsMenuButton.IsCheckedDelegate isChecked)
            {
                if (__instance.mPage == OptionsMenu.Page.Main)
                {
                    if (buttonCount == 0) // only do this once, bit of a hack
                        MissingSongsUI.SetMenu(__instance);
                }
            }
        }
    }
}