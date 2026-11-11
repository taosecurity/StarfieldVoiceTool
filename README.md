![Workflow](https://github.com/taosecurity/StarfieldVoiceTool/actions/workflows/dotnet-desktop.yml/badge.svg?branch=main)  [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# Starfield Voice Tool
Browse every voiced line in Starfield and in your installed mods, by plugin and voice type; search the dialogue text, listen to any line and save it as a `.wav`. Useful for finding a companion's lines, checking what a mod's voice files say, or getting audio clips for your own mod or video. The **Sound Effects** tab does the same for the game's and your mods' sound effects and music: about 35,000 sounds by name, ID and Wwise event.

**First try:** download the zip for your system from [Releases](https://github.com/taosecurity/StarfieldVoiceTool/releases), extract it, and run `StarfieldVT.exe`. Steam installs are found automatically; otherwise point it at your game's `Data` folder. Once loading finishes, pick **Starfield.esm** on the left, type `captain` in the filter box under the lines, and double-click a line to hear it.

This is a fork of [yak3d/StarfieldVoiceTool](https://github.com/yak3d/StarfieldVoiceTool) with bug fixes and a sound effects browser; see [Changes](#changes-in-090).

**For AI assistants:** see [AGENTS.md](AGENTS.md) for install steps, options, log locations and limits.

## Requirements
* Starfield must be installed. Steam installs are detected automatically; otherwise you'll be asked for the game's `Data` folder (the one containing `Starfield.esm`).
* Nothing else on Windows: playback and export work out of the box, with no extra downloads.
* On Linux, see [Linux](#linux).

## Installation
1. Download a [Release Zip](https://github.com/taosecurity/StarfieldVoiceTool/releases) or a [Nightly Zip](https://github.com/taosecurity/StarfieldVoiceTool/actions)
2. Extract to a folder of your choice

## Running
1. Ensure your load order in `plugins.txt` or your Mod Manager is set with the plugins you want the tool to scan. If there is no `%LOCALAPPDATA%\Starfield\Plugins.txt` (for example, mods copied into `Data` by hand), the tool reads every plugin in the `Data` folder.
2. Run `StarfieldVT.exe` (`StarfieldVT` on Linux). The first start takes a while as it goes through your load order and finds the dialogue lines; progress is shown at the bottom of the window.
3. The results are cached in `%LOCALAPPDATA%\StarfieldVT\` (`cache.json` plus a search index in `index\`).
   * The cache is rebuilt automatically when the tool is updated, or when the game's plugins, archives, data folder or load order change (and loose voice files, with the mod voices option on).
   * **File** → **Delete Cache** forces a rebuild.
4. Select a plugin or voice type on the left. Double-click a line to play it, or select it and press **Export** to save it as a `.wav` wherever you choose. Drag the divider between the list and the table to resize them; double-click the edge of a column header to fit that column.
5. **Mod voices.** The tool reads voice files from every archive (archives localized to other languages are skipped) and from loose `.wem` files in `Data\Sound\Voice`. Many mods and Creations pack their voices in an archive such as `MyMod - Main.ba2` instead of `MyMod - Voices_en.ba2`; Kinggath's Watchtower (`kinggathcreations_spaceship.esm`) is one. Loose files take priority over archives, as in the game. On a PC with very many mods you can save a few seconds of loading by turning off **Options** → **Include voices from all mod archives and loose files**, which reads only `<Name> - Voices….ba2` archives.
6. Plugins are listed by file name, so a Creation may appear under a name that differs from its store title (Watchtower is `kinggathcreations_spaceship.esm`). Only plugins with voiced lines are listed.
7. The filter box under the lines searches the dialogue text. It supports [Lucene syntax](https://lucene.apache.org/core/4_8_0/queryparser/org/apache/lucene/queryparser/classic/package-summary.html#package_description) such as `"exact phrase"`, `capt*` and `ship AND crew`. With nothing selected, it searches every plugin.

## Sound effects
The **Sound Effects** tab lists every Wwise sound the game can load: the sound effects, ambience and music in `Starfield - WwiseSounds*.ba2`, and the sounds mods and Creations add in their own archives (Shattered Space, Watchtower and Trackers Alliance among them) or as loose files in `Data\Sound\Soundbanks`. The list is read the first time you open the tab, which takes a few seconds, and is kept until you close the tool.

1. Pick a group on the left: the base game's sounds by their Wwise folder (`AMB` ambience, `FST` footsteps, `MUS` music, `NPC` creatures and people, `UI`, `VEH` ships and vehicles, `WPN` weapons, ...), or one mod's sounds (**Mod: …**).
2. Type in the search box to narrow the list. Each word must match the sound's name, folder, bank or one of its events, or be its full ID: `lightsaber`, `80667371`, `MUS_AstralLounge`, `wind exteriors`.
3. Double-click a sound (or press ▶) to hear it. **Export** saves the selected sound as a `.wav`; with several selected, it asks for a folder and names each file `<Name>_<ID>.wav`.
4. **Copy ID** and **Copy Events** put the selected sounds' Wwise IDs or event names on the clipboard, for use in your own mod or in the Creation Kit.

The columns: **ID** is the Wwise ID (the sound's file is `sound\soundbanks\<ID>.wem`), **Events** are the Wwise events that can play it, **Bank** is the sound bank that lists it, **Source** is the archive it is read from (for a sound stored inside a sound bank, also the bank's file, e.g. `Starfield - WwiseSoundsPatch.ba2, inside 2286754810.bnk`), and **Replaces** names earlier archives that hold the same ID. When two archives hold the same sound, the one loaded later wins, as in the game: the base game's `WwiseSoundsPatch` archive replaces about 2,000 older sounds, and a mod can replace any sound the same way.

The names and events come from the JSON files Wwise writes next to each sound bank, which the game and most mods ship. A sound that no bank names is listed by its ID in the **Other** group.

Most sounds are separate `.wem` files, but a bank (`.bnk`) can also hold short sounds inside itself; in the base game these are the player character's sprint breathing (97 sounds in the `VOC` group). These are listed too. Two other kinds of data inside banks are left out because they aren't separate sounds: the opening of a sound whose full `.wem` is already listed (Wwise keeps it in the bank so playback can start at once), and data for Wwise effects such as reverb.

## Linux
Extract the Linux zip and run `./StarfieldVT`. Playback uses the system's VLC library, and minimal installs may lack the X11 and font libraries the window needs. On Debian or Ubuntu:

```
sudo apt install libvlc5 vlc-plugin-base libfontconfig1 libice6 libsm6 libx11-6 libx11-xcb1
```

* Native Steam and Proton installs are detected automatically. Otherwise, type or paste the game's `Data` folder in the dialog that appears; **Browse…** works where the desktop provides a folder picker (xdg-desktop-portal or GTK).
* The tool reads every plugin in the `Data` folder, so it may list more plugins than are active in your game.
* Settings, cache and logs are in `~/.local/share/StarfieldVT/`.
* WSL works too, with the game's Windows install under `/mnt/c/...`. Loading is slower there, because WSL reads Windows files through a network-style link.

## Troubleshooting
**Help** → **Open Log Folder** opens the log folder, and **Help** → **About Starfield Voice Tool** → **Copy details** copies the version, system, Data folder and log location for a bug report.

The tool always writes a log: `%LOCALAPPDATA%\StarfieldVT\logs\starfieldvt<date>.log` on Windows, `~/.local/share/StarfieldVT/logs/` on Linux. One file per day; the last 5 are kept. The log records the version, the Data folder and load order it read, which plugins it skipped and why, each archive read, and every playback or export error.

For more detail, start the tool from a command prompt (or a shortcut) with these options:

| Option | What it does |
|---|---|
| `--verbose` (or `-v`) | Also logs every quest and dialogue topic parsed, every search, each step of converting a voice file or sound, and how many sounds each archive holds |
| `--log <file>` | Writes the log to `<file>` instead of the daily file, e.g. `--log C:\temp\starfieldvt.log` |

For example, in a command prompt or PowerShell window opened in the tool's folder: `.\StarfieldVT.exe --verbose --log C:\temp\starfieldvt.log` (Linux: `./StarfieldVT --verbose --log /tmp/starfieldvt.log`). Attach that file when you report a problem.

If a plugin's lines are missing, search the log for `skipped` or `Skipping`. `Plugins.txt` is shared by every Starfield install on the PC, so with more than one install (or a mod manager pointed at another folder) it can list plugins that aren't in the `Data` folder you chose. The tool skips those, and the log lists them.

If the Sound Effects tab shows fewer sounds than expected, search the log for `Found` (the line `Found N sounds (M from mods ...)`) and, with `--verbose`, for each archive's count; an archive or bank that can't be read is logged with `Could not read archive` or `Could not read bank`.

If the voice lines look wrong or stale, use **File** → **Delete Cache**, or delete `%LOCALAPPDATA%\StarfieldVT\cache.json` and the `index` folder.

Please report issues at https://github.com/taosecurity/StarfieldVoiceTool/issues

## License
The source code in this repository is licensed under the [MIT License](LICENSE).

The release builds bundle third-party libraries, including [Mutagen](https://github.com/Mutagen-Modding/Mutagen), which is licensed under the GPL-3.0. The compiled program as a whole is therefore distributed under the GPL-3.0. [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) lists every bundled component with its license and source, and the full license texts are in [licenses](licenses). Both are included in every release zip.

## Changes in 0.9.1
* The Sound Effects tab also lists sounds stored inside sound bank (`.bnk`) files, not only separate `.wem` files. In the base game that adds 97 sounds (the player character's sprint breathing, in the `VOC` group); mods that keep short sounds in their banks show them too. They play and export like any other sound, and the **Source** column names the bank they're in.
* Fixed: loading failed with "Mod was both a light and medium master" when any plugin in the load order had both the light (ESL) and the medium flag set in its header, and the message didn't say which plugin. That plugin is now skipped (along with any plugin that needs it), the log names it, and everything else loads. To use the plugin's lines, clear one of the two flags in xEdit or the Creation Kit.
* Because the version changed, the voice line cache rebuilds once on first start.

## Changes in 0.9.0
* New **Sound Effects** tab: browse, search, play and export the game's and your mods' sound effects and music (about 35,000 sounds on a Steam install with Shattered Space and a few Creations). See [Sound effects](#sound-effects). The idea comes from [starfield-wwizard](https://github.com/yak3d/starfield-wwizard) by yak3d, the original author of this tool; this version is written from scratch from the file formats (no code was taken from it) and adds mod sounds, export, events and Linux support.
* Sounds stored as Wwise PCM (more than half of the sound effects) are now converted too. Voice lines are unchanged: they all use Wwise Vorbis, which goes through the same converter as before.
* A sound in a format the tool can't convert now gives a message naming the format, instead of a general conversion error.
* Fixed in the bundled converter (our BnkExtractor fork): Wwise Vorbis sounds with loop points, such as ambient loops and some music, failed with "Could not read full string!", because the converter wrote one of the loop comments in the Ogg header with the wrong length. Voice lines have no loop points and were never affected.
* Because the version changed, the voice line cache rebuilds once on first start.

## Changes in 0.8.1
* Fixed: the dialogue filter under the table ignored the voice type filter on the left once a plugin was selected. For example, with `sarahmorgan` on the left, a plugin selected and `weapons` in the dialogue filter, every voice type's "weapons" lines in that plugin were shown. The dialogue filter now searches only the lines the table is showing, so all three narrow together: plugin, voice type filter and dialogue text. The status line says what the table is limited to.
* New, original program icon: a speech bubble with a sound wave on a starry background. It replaces the face icon inherited from the original tool, whose source was unknown. The icon is drawn from scratch by [`art/make_icon.py`](art/make_icon.py) (no game art) and is covered by this repository's MIT license.
* The window shows the icon on Linux too (before, only the Windows `.exe` had one).
* The plugin list on the left can be resized: drag the divider between it and the table. The tool remembers the width.
* Double-click the divider at the right edge of a column header (Voice Type, Filename, Dialogue) to fit that column to its contents, as in Windows Explorer. A double-click on a header no longer plays the selected line.
* New **Help** menu: **Open Log Folder**, **Report a Problem…** (opens the GitHub issues page) and **About Starfield Voice Tool**. The About window shows the version, credits and license, links to the project and the third-party notices, the Data folder and settings folder in use, and a **Copy details** button for bug reports. The tool itself still never goes online; links open in your browser.
* Because the version changed, the voice line cache rebuilds once on first start.

## Changes in 0.8.0
* Fixed: with a `Plugins.txt` that lists plugins missing from the chosen `Data` folder (a second Starfield install, or a mod manager's load order for another folder), loading failed with "Failed to load voice types" and no lines were shown. Those plugins, and any plugin whose masters are missing, are now skipped and listed in the log.
* Fixed: mods built on a Creation (for example Terran Armada patches, which need `SFBGS050.esm`) made loading fail the same way, because the game loads Creations from its own catalog rather than `Plugins.txt`. Such masters are now loaded when they are in the `Data` folder, and the log lists them.
* **Include voices from all mod archives and loose files** is now on by default, and switched on once for existing settings (you can turn it off again). Without it, mods and Creations that pack voices in a ` - Main` archive, such as Astrid and Kinggath's Watchtower, showed no lines.
* Fixed: each played or exported line left the temporary `.wem` and the Vorbis codebook file open (a bug in the bundled BnkExtractor's ww2ogg port, now fixed in [taosecurity/BnkExtractor](https://github.com/taosecurity/BnkExtractor)). A failed conversion no longer leaves a half-written `.ogg` behind.
* New command-line options `--verbose` and `--log <file>` for troubleshooting; see [Troubleshooting](#troubleshooting). Voice file conversion errors, which used to go only to a console window, are now in the log, as are unexpected errors that close the tool.
* The release zips include this README and [AGENTS.md](AGENTS.md).

## Changes in 0.7.0
* New option **Options** → **Include voices from all mod archives and loose files**: finds mod voices packed in archives without ` - Voices` in the name (for example `MyMod - Main.ba2`) and loose `.wem` files under `Data\Sound\Voice`. Off by default. Turning it on or off rebuilds the cache.
* Without a `Plugins.txt` (mods copied into `Data` by hand, no mod manager), the tool now reads every plugin in the `Data` folder instead of only the game's built-in ones. Plugins are ordered after their masters; a plugin whose master is missing is skipped and noted in the log.

## Changes in 0.6.3
Linux fixes, found by testing the Linux build for the first time:
* The Linux build no longer crashes at startup on systems without libicu, such as minimal Debian or WSL installs.
* Setting the game path no longer depends on a system folder picker. The dialog lets you type or paste the path, and **Browse…** uses the picker where one exists. Without a picker (no xdg-desktop-portal or GTK), the first-run setup used to wait forever.
* Native Steam and Proton installs of Starfield are detected automatically on Linux.
* Playback works with the system VLC from the normal runtime package (`libvlc5`). It previously looked for `libvlc.so`, which only the developer package provides. If VLC is missing, the error now says what to install.
* Every line plays reliably, short ones included. A long-lived VLC instance went silent after the first sound on some audio setups (seen on WSLg), and short lines could be cut off when playback ended. This affected Windows too, with high-latency audio such as Bluetooth.
* New [Linux](#linux) section listing the packages to install.

## Changes in 0.6.2
* **ffmpeg is no longer needed or downloaded.** Voice files are now decoded inside the app ([NVorbis](https://github.com/NVorbis/NVorbis)), which removes the first-start download of a third-party ffmpeg build. The output was checked against ffmpeg's on 300 voice lines from every plugin: identical. A copy downloaded by an earlier version in `%LOCALAPPDATA%\StarfieldVT\ffmpeg` is no longer used and can be deleted.
* Temporary audio files are deleted right after each conversion (a file handle left open by the ww2ogg converter previously kept them until the next start).
* Release zips include `LICENSE.txt`, `THIRD-PARTY-NOTICES.md` and a `licenses` folder with the license texts of all bundled components, with links to the exact source of the GPL and LGPL ones.
* Removed an unused icon library (Projektanker.Icons.Avalonia.FontAwesome).

## Changes in 0.6.1
* Release zips extract with the app at the top level, instead of inside an `artifacts\<name>` folder.
* The Linux `StarfieldVT` binary in the release zip is executable again; previously it needed `chmod +x`.
* The build pipeline uses current GitHub Actions versions.

## Changes in 0.6.0
* **Export** asks where to save the `.wav`. It used to write into the game's `Data\sound\voice` folder without asking, and crashed if that failed.
* The game path is set up before loading starts. Previously the first run failed if the game wasn't auto-detected, and changing the path didn't reload anything.
* The progress bar tracks the real quest count per plugin. It used to say "Loaded" after 100 quests, and stayed visible forever when the cache was used.
* Fixed a race that could cut off a new line when the previous one finished playing.
* Typing incomplete search syntax (e.g. a lone `"` or `(`) no longer breaks the filter.
* Voice archives are chosen by name pattern: English (`_en`) or language-neutral archives only. Before, a mod named like another plugin, or containing "en", could pull in the wrong archives or languages. Voice folders for `.esl` plugins no longer abort loading.
* Loading is much faster: the voice archives are indexed once instead of once per plugin, lines are no longer copied on every insert, and selecting a large plugin no longer rebuilds lists repeatedly.
* Playback and export run in the background, so the window doesn't freeze. Temporary audio files are cleaned up.
* The cache is invalidated when the game files or load order change, not only on a tool update. It's also written atomically.
* Errors (load failures, playback/export problems, ffmpeg download failures) are shown in the window, not just in the log.
* Removed dead code and added unit tests for the core parsing, cache and search logic.
