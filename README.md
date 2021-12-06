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