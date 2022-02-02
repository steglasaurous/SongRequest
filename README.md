# SongRequest
!asr command for song requests in twitch chat.
Allows viewers to request songs from the streamer's local library and, when used with SongBrowser version 2.3.2 or newer, all published custom songs.

## Use
Find a song you want to request [here](https://maudica.com/), then click the twitch symbol in the song's panel to copy the request text to your clipboard. Next, paste that text into twitch chat and send the message to request the song. It will look something like this:
> !asr -id 744

Use **!oops** to remove your latest request in case you don't want to keep it.

## Advanced Use
Following alternative options are available:

| Command                                       | Example                              | Result                                          |
|-----------------------------------------------|--------------------------------------|-------------------------------------------------|
|!asr *title*                                   |!asr Those Who Fight                  | Request Those Who Fight Further                 | 
|!asr *title* -mapper *mapper*                  |!asr Monster -mapper Octo             | Request Monster mapped by Octo                  |
|!asr *title* -artist *artist*                  |!asr MONSTER -artist REOL             | Request MONSTER by REOL                         |
|!asr *title* - artist *artist* -mapper *mapper*|!asr Man -artist Rihanna -mapper Draco| Request Man Down by Rihanna, mapped by DeadDraco|

Note that search is not case sensitive, so `!asr Monster` and `!asr MONSTER` give the same result unless the mapper or artist is explicitly included in the command (the way it is done in the examples above).                      | 

**Mods and the channel owner** can remove any request using **!remove** (comes with the same variations as !asr, so e.g. !remove Monster -mapper Octo).

| Command                                          | Example                                 | Result                                          |
|--------------------------------------------------|-----------------------------------------|-------------------------------------------------|
|!remove *title*                                   |!remove Those Who Fight                  | Remove Those Who Fight Further                  | 
|!remove *title* -mapper *mapper*                  |!remove Monster -mapper Octo             | Remove Monster mapped by Octo                   |
|!remove *title* -artist *artist*                  |!remove MONSTER -artist REOL             | Remove MONSTER by REOL                          |
|!remove *title* -artist *artist* -mapper *mapper* |!remove Man -artist Rihanna -mapper Draco| Remove Man Down by Rihanna, mapped by DeadDraco |

They can also requests songs with **!asr** even when the queue is closed and open/close the queue using **!enableQueue** or **!disable Queue**, respectively.
If the ModSettings mod is installed, the special mod privileges can be individually en-/disabled in the settings. They are enabled by default.

## AudicaWebsocketServer Integration

Optionally, if the AudicaWebsocketServer mod is installed at v1.1.0 or higher, bot responses will be emitted as 
websocket events.  

The following events will be emitted:

**SongNotFound**

When an `!asr` request fails to find a song.

```json
{
  "eventType": "SongNotFound",
  "data": {
    "Title": "badsong",
    "Artist": null,
    "Mapper": null,
    "MaudicaID": null,
    "RequestedBy": "517290525",
    "RequestedAt": "2022-02-01T22:58:26.800836-05:00",
    "FullQuery": "badsong"
  }
}
```

**RemoveSongQueueItem**

When `!yeet` or `!remove` are used to remove a song.

```json
{
  "eventType": "RemoveSongQueueItem",
  "data": {
    "SongID": "Kepler-SgtCrowMix_38ec025be763601c8295ebeede9314c3",
    "Title": "Kepler",
    "Artist": "Pythius",
    "Mapper": "SgtCrowMix",
    "RequestedBy": "517290525",
    "RequestedAt": "0001-01-01T00:00:00"
  }
}
```

**QueueEnabled**

When `!enablequeue` is used to enable the queue.

```json
{
  "eventType":"QueueEnabled",
  "data":""
}
```

**QueueDisabled**

```json
{
  "eventType":"QueueDisabled",
  "data":""
}
```

**NewSongQueueItem**

When an `!asr` request is successfully added to the queue.

```json
{
  "eventType": "NewSongQueueItem",
  "data": {
    "SongID": "Kepler-SgtCrowMix_38ec025be763601c8295ebeede9314c3",
    "Title": "Kepler",
    "Artist": "Pythius",
    "Mapper": "SgtCrowMix",
    "RequestedBy": "517290525",
    "RequestedAt": "2022-02-01T23:07:20.3035806-05:00"
  }
}
```

**SongAlreadyInQueue**

If the song requested via `!asr` is already in the queue.

```json
{
  "eventType": "SongAlreadyInQueue",
  "data": {
    "SongID": "Kepler-SgtCrowMix_38ec025be763601c8295ebeede9314c3",
    "Title": "Kepler",
    "Artist": null,
    "Mapper": null,
    "RequestedBy": null,
    "RequestedAt": "0001-01-01T00:00:00"
  }
}
```

## Installation
* Download latest release from [here](https://github.com/Silzoid/SongRequest/releases/latest) or use the [Mod Browser](https://github.com/Contiinuum/ModBrowser/releases/latest) mod
* If you downloaded the latest release manually, save the .dll file to [YourAudicaFolder]\Mods
* Enable the in-game twitch chat
  * Go to Settings - Spectator Cam
  * Scroll to the *Twitch Chat View* section
  * Enable *Twitch Chat View*
  * Enter the name of your channel in *Twitch Channel:*
* Optionally: Install the [SongBrowser mod](https://github.com/Silzoid/SongBrowser/releases/latest) version 2.4.1 or newer to enable requests for custom songs that haven't been downloaded yet

The queue is preserved when the game is closed or crashes and will be available on next launch.