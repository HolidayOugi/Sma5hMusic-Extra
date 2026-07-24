# Sma5hMusic Extra

This is a modified version of [Sma5shMusic by Deinonychus71](https://github.com/Deinonychus71/Sma5hMusic) that adds extra functionality to the original software.

> [!WARNING]
> This tool is experimental, please make a backup of your music files and jsons before using it.

Like the original release, a dump of the following files from `data.arc` is needed:

* `bgm_property.bin`
* `msg_bgm+[region].bin`
* `msg_title+[region].bin`
* `ui_bgm_db.prc`
* `ui_gametitle_db.prc`
* `ui_series_db.prc`
* `ui_stage_db.prc`

After extraction, the files are to be copied (with their directories) in the `Resources/Game` directory.

A full tutorial on how to use Sma5hMusic can be found [here](https://gamebanana.com/tuts/13677).

Here's a rundown of the added features.

## CSK Pack Building

> [!NOTE]
> To run the CSK Music Packs in-game both [ARCropolis](https://github.com/raytwo/arcropolis) and the [CSK Collection](https://gamebanana.com/mods/499008) have to be installed on your system.


**CSK-compatible Music Packs** can be now be built. The software offers two options:

* **Single Pack**: A single Music Pack comprised of all the Mods and Series currently loaded.
* **Modular Packs**: Multiple Modular Music Packs for each Series and Mod. An option is given to select which Series to generate. 

![Series Selection](https://elixi.re/i/bfyok.png)

> [!TIP]
> When using the Modular Packs option, if one or more Vanilla Series were not selected during the generation, an optional Series Order pack is generated. If loaded, it ensures that each Series is in its correct order in the Music Select / Sound Test screen.

> [!TIP]
> When using the Modular Packs option, if one or more Core songs had their values changed and they weren't from a Series already selected during the generation, an optional Vanilla Song Changes pack is generated. If loaded, it will apply the changes made to the edited Core Songs.

## Icon Selection and Conversion

**Icons** can now be assigned to Series directly from the Create Series / Edit Series menu. The Software accepts PNGs or BNTXs as input and assigns them to a Series, converting them if needed. The Icons are automatically copied to the output build.

![Icon Selection](https://elixi.re/i/t0jjq.png)

> [!TIP]
> To obtain the best results, please choose a square icon with a transparent background and an all-white texture.

## Importing of Audio Files

**Standard Audio files** (.mp3, .flac, .wav, .ogg, .m4a) can now be loaded directly into the software without prior conversion.

When loaded, the loop points for the given song can be selected. They can then be previewed by playing the track itself slightly before and after each loop point. The audio file is finally converted using the chosen loop points.

![Loop Point Selection](https://elixi.re/i/qj2ap.png)

> [!TIP]
> The preview duration can be set in the Global Settings.

### Automatic Loop Detection

> [!NOTE]
> Requires the installation of [PyMusicLooper](https://github.com/arkrow/PyMusicLooper) and for it to be accessible in PATH.

The software can also automatically detect potential loop points, making it easy to preview and choose from the suggested loops.

![PyMusicLooper](https://elixi.re/i/b8l0c.png)

> [!TIP]
> This feature can be used from the BGM Properties tab on songs that have already been added!

## Normalization of Songs

> [!NOTE]
> Requires the download of [FFmpeg](https://www.ffmpeg.org/) and for its path to be set in the Global Settings.

Songs can now be **normalized** to a certain LUFS normalization level. This can be applied in three ways.

* **At Import Time**: when adding a song, either already converted or not, a checkbox can be checked for Normalization.
* **In BGM Properties**: a new option is available in the BGM Properties tab to normalize songs that've already been added.
* **Batch Normalization**: in the Extra submenu, a new option can be chosen to normalize all of the songs already added to the software.

![Normalization](https://elixi.re/i/xat1c.png)

> [!TIP]
> The LUFS Normalization value can be set in the Global Settings.

> [!TIP]
> After normalization, the songs will be converted to NUS3Audio if they were in a different format.

## Direct YouTube Download support

> [!NOTE]
> Requires the download of [FFmpeg](https://www.ffmpeg.org/) and [yt-dlp](https://github.com/yt-dlp/yt-dlp) and for their paths to be set in the Global Settings.

Songs can now be directly downloaded from **YouTube** and added to the software in a single action.

The songs can be imported either from their URL or from a text file containing all of the links, one per line.

![YouTube](https://elixi.re/i/ygkah.png)

> [!TIP]
> Playlists are also supported. The software will notify that all the songs from a given playlist will be downloaded.

## Colored Text

Colored Text is now supported when editing a song title. The color can either be chosen from a premade list of colors or by inputting the hex value of a custom color. If part of the text is highlighted before selecting a color, the highlighted text will be recolored.

![Colored](https://elixi.re/i/t07dd.png)

The chosen colors will display in-game in the Music Select screen.

![In-Game](https://elixi.re/i/zxppl.png)

> [!TIP]
> It's recommended to choose colors that are visibile on both light and dark backgrounds, otherwise they might be unreadable in certain contexts.

## Replace Core Songs

Core Songs can now be replaced in order to generate WiFi-Safe music packs. When importing a song, either from a NUS3AUDIO or an audio file, there'll be an option to choose a core song to replace.

![Core Songs](https://elixi.re/i/bia5p.png)

After importing, replaced songs will be denoted in the main window with gold text, to differentiate them from added songs.

![Gold](https://elixi.re/i/5svqc.png)

> [!WARNING]
> To keep the pack wifi-safe, refrain from editing playlist/stage data. Keep the edits only to song names and audio.

> [!TIP]
> If no changes to the song order and playlist data were made, the `sound` and `ui/param` folders (when building with the standard Build) or the `database` folder (when building with the CSK build) can be safely deleted.

## Generate Victory Themes

> [!NOTE]
> Requires the latest [CSK Collection](https://gamebanana.com/mods/499008) update released in July 2026.

Custom Victory Themes can now be generated from an option in the Extra submenu.

Victory themes can be generated for both base-game and custom characters. When generating a theme for a base-game character, there's the option to either replace the default victory theme or assign a custom Tone ID to it, allowing characters that normally share a victory theme to use separate ones.

![Victory](https://elixi.re/i/o3oru.png)

## Miscellaneous fixes and improvements

* (*Should have*) fixed bug where songs would stop playing in-game due to their order in the global song list.
* Packs now are output in a subfolder of the build folder. This can be disabled in Global Settings.
* The default volume for a new song can now be set in Global Settings.
* Volume can now be set to the mean or median value of all songs' volume in a Mod.
* The Song List can now be exported to a spreadsheet.
* [Experimental] Added option to generate a Sma5hMusic Mod from an already generated build.
* Small text is now shown directly in the GUI.
* A display box has been added to the BGM Properties Window to show how the text will appear in game, with both colors and small text.
* A button has been added to the BGM Properties Window to automatically add Small text brackets. If a portion of text is highlighted when pressing the button, it will apply the brackets to that portion of text.
* Added a checkbox in the Series Properties Window to enable/disable the "Series" suffix after the Series name in Music Select.
* Core Songs volume can now be changed in app and the relevant nus3bank will be generated at build time. This can be disabled in Global Settings.
* Songs can now be sorted alphabetically per game or per series automatically.
* If a song from custom Series was not manually added to a playlist, it will be automatically added to the Battlefield playlist to ensure it shows up in-game.
* Global Settings can now be saved when output folder is missing.
* Fixed importing of files with Japanese characters not working.
* Fixed a possible silent conversion fail with BRSTM/IDSP/LOPUS files at build time.
* Fixed importing of overrides from older Sma5hMusic builds having invalid characters.
* Fixed some UI elements not showing properly at lower screen resolutions.
* Fixed tone ID validation not always checking if the value is already present.
* Fixed song list not refreshing when a filter was active.

## FAQ

### What are CSK Packs and how are they different from normal music packs?

The Standard legacy build option included in the original version of Sma5hMusic rebuilds the databases and message files from the ground up, appending the new additions to them.

By contrast, the CSK build option generates a JSON file containing all of the changes which are then to be patched in real-time while the game is booting up.

Compared to a standard build, the CSK Music Packs have the following advantages:

* Compatible with other CSK Music Packs or mods that edit the prc database files.
* Better load times with bigger music packs.
* Easily editable in notepad for small changes.
* Can be easily split into smaller packs.

### I have an old Sma5hMusic setup on my PC, how do I convert my mods to the CSK Format?

Just load the Mods in Sma5hMusic Extra and build a CSK Pack(s) from the Project submenu. If Sma5hMusic Extra was extracted to a new folder, just copy the Mods folder over from the old setup.

### Where can I find yt-dlp and ffmpeg?

[Here](https://github.com/yt-dlp/yt-dlp) and [here](https://github.com/btbn/ffmpeg-builds). Then, set the path to their executable in Global Settings.

### How do I install Pymusiclooper?

Follow the instructions detailed on its [GitHub page](https://github.com/arkrow/PyMusicLooper), it should then be recognized automatically. To make sure it was installed correctly, typing `pymusiclooper` into a terminal window should show all of the software options.

## Generate a Release build

```
dotnet publish Sma5hMusic.GUI\Sma5hMusic.GUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Issues

If you encounter any problems using the software, submit an issue here on GitHub.

## To-Do List

- [x] Update vgmstream library and main executable to x64
- [ ] Decouple CSK Build service from JSONs loading
- [ ] Allow editing of Color Picker color list
- [ ] Add Trim function when importing from audio
- [ ] Add Filter to MainWindow for songs not in a playlist
- [ ] Allow reverse importing of CSK Packs and/or XMSBT files
- [ ] One Slot Victory Themes support

## Thanks & Repos of the different tools
1.  Original Code and Author: Deinonychus71
2.  Research: soneek
3.  Testing: Demonslayerx8, Segtendo
4.  Original Icon: Segtendo
5.  Testing and Support (Extra): zyrskyd, Segtendo, CorbataLM, Mika, vernonviper, Kagura101, Char
6.  prcEditor: https://github.com/BenHall-7/paracobNET - BenHall-7
7.  paramLabels: https://github.com/ultimate-research/param-labels - BenHall-7, jam1garner, Dr-HyperCake, Birdwards, ThatNintendoNerd, ScanMountGoat, Meshima, Blazingflare, TheSmartKid, jugeeya, Demonslayerx8
8.  msbtEditor: https://github.com/IcySon55/3DLandMSBTeditor - IcySon55, exelix11
9.  nus3audio: https://github.com/jam1garner/nus3audio-rs - jam1garner
10.  bgm-property: https://github.com/jam1garner/smash-bgm-property - jam1garner
11.  VGAudio: https://github.com/Thealexbarney/VGAudio - Thealexbarney, soneek, jam1garner, devlead, Raytwo, nnn1590
12.  vgmstream: https://github.com/vgmstream/vgmstream - bnnm, kode54, NicknineTheEagle, bxaimc, Thealexbarney
All contributors: https://github.com/vgmstream/vgmstream/graphs/contributors
13. SoX: https://sox.sourceforge.net/ - SoX contributors
14. PyMusicLooper: https://github.com/arkrow/PyMusicLooper - arkrow and contributors
15. CrossArc: https://github.com/Ploaj/ArcCross Ploaj, ScanMountGoat, BenHall-7, shadowninja108, jam1garner, M-1-RLG
16. yt-dlp: https://github.com/yt-dlp/yt-dlp - yt-dlp contributors
17. FFmpeg: https://github.com/FFmpeg/FFmpeg - FFmpeg contributors
