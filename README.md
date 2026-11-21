![Workflow](https://github.com/taosecurity/StarfieldVoiceTool/actions/workflows/dotnet-desktop.yml/badge.svg?branch=main)  [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# Starfield Voice Tool
Browse every voiced line in Starfield and in your installed mods, by plugin and voice type; search the dialogue text, listen to any line and save it as a `.wav`. Useful for finding a companion's lines, checking what a mod's voice files say, or getting audio clips for your own mod or video. The **Sound Effects** tab does the same for the game's and your mods' sound effects and music: about 35,000 sounds by name, ID and Wwise event. Both tabs can export many lines or sounds at once, and the `starfieldvt-export` command does the same from a script.

**First try:** download the zip for your system from [Releases](https://github.com/taosecurity/StarfieldVoiceTool/releases), extract it, and run `StarfieldVT.exe`. Steam installs are found automatically; otherwise point it at your game's `Data` folder. Once loading finishes, pick **Starfield.esm** on the left, type `captain` in the filter box under the lines, and double-click a line to hear it.

This is a fork of [yak3d/StarfieldVoiceTool](https://github.com/yak3d/StarfieldVoiceTool) with bug fixes, a sound effects browser and bulk export; see [Changes in 0.10.0](#changes-in-0100) and [CHANGELOG.md](CHANGELOG.md).

**For AI assistants:** see [AGENTS.md](AGENTS.md) for install steps, options, log locations and limits.

## Example
This lists the microgun sounds a bulk export would write, without writing anything. It runs in a command prompt opened in the tool's folder.

```
> starfieldvt-export sounds --list --group WPN --search microgun
Reading sound archives in C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data...
Found 35491 sounds (6914 from mods, 97 inside banks, 1 without a name) in 19 sources in 1.3 seconds
100 sounds would be exported (game folders, list.csv on):
  WPN\Hand\Microgun\WPN_Hand_AlliedMicrogun_FireEnd_NPC_Auto_LayerB_01_811909741.wav  (sound 811909741 from Starfield - WwiseSounds04.ba2)
  WPN\Hand\Microgun\WPN_Hand_AlliedMicrogun_FireEnd_NPC_Auto_LayerB_02_613599831.wav  (sound 613599831 from Starfield - WwiseSounds03.ba2)
...
  ... and 80 more
```

Leave out `--list` and add `--out <folder>` to write the `.wav` files; see [Bulk export](#bulk-export).

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
3. Double-click a sound (or press ▶) to hear it. **Export** saves the selected sound as a `.wav`. With several selected, or with **Export All Shown (N)**, it exports them all to a folder; see [Bulk export](#bulk-export).
4. **Copy ID** and **Copy Events** put the selected sounds' Wwise IDs or event names on the clipboard, for use in your own mod or in the Creation Kit.

The columns: **ID** is the Wwise ID (the sound's file is `sound\soundbanks\<ID>.wem`), **Events** are the Wwise events that can play it, **Bank** is the sound bank that lists it, **Source** is the archive it is read from (for a sound stored inside a sound bank, also the bank's file, e.g. `Starfield - WwiseSoundsPatch.ba2, inside 2286754810.bnk`), and **Replaces** names earlier archives that hold the same ID. When two archives hold the same sound, the one loaded later wins, as in the game: the base game's `WwiseSoundsPatch` archive replaces about 2,000 older sounds, and a mod can replace any sound the same way.

The names and events come from the JSON files Wwise writes next to each sound bank, which the game and most mods ship. A sound that no bank names is listed by its ID in the **Other** group.

Most sounds are separate `.wem` files, but a bank (`.bnk`) can also hold short sounds inside itself; in the base game these are the player character's sprint breathing (97 sounds in the `VOC` group). These are listed too. Two other kinds of data inside banks are left out because they aren't separate sounds: the opening of a sound whose full `.wem` is already listed (Wwise keeps it in the bank so playback can start at once), and data for Wwise effects such as reverb.

## Bulk export
Both tabs can save many voice lines or sounds as `.wav` files in one go, and the `starfieldvt-export` command does the same without the window.

### In the window
The bar under each tab's table has these controls:

* **Export** saves the selected row as a `.wav` wherever you choose. With several rows selected, it asks for a folder and exports them all. In Voice Lines, Ctrl+click adds or removes a line and Shift+click selects a range; a double-click still plays the line you clicked.
* **Export All Shown (N)** exports every row the table shows, N of them: the plugin, voice type or group picked on the left, narrowed by the search or filter box. Pick a folder, and the export starts.
* **Keep game folders** (on by default) keeps the game's layout: sounds go to `<Group>\<Folder>\<Name>_<ID>.wav` (for example `WPN\Hand\Microgun\...`), sounds that no bank names to `Unnamed\<ID>.wav`, and voice lines to `<Plugin>\<Voice type>\<Voice type>_<file>.wav`. Off, every file goes into the one folder you chose.
* **Write list.csv** (on by default) writes `list.csv` in that folder, one row per file. The two check boxes are shared by both tabs and remembered.

While an export runs, a progress bar and a **Cancel** button replace the buttons, and the line under the table counts the files. Only one export runs at a time. **Cancel** stops after the current file, and `list.csv` lists what was written up to then.

An export of more than 1,000 files first writes 50 of them, then asks whether to go on. The question gives a size estimate from those 50 files and the drive's free space, and warns when the space may not be enough. If you say no, the 50 files and their `list.csv` stay in the folder.

Files that are already in the export folder are kept, not written again, so running the same export a second time only adds what is missing. A file is written under a temporary `.part` name first and renamed when it is complete, so a stopped export never leaves a broken `.wav` behind. Names from mods that Windows doesn't allow in a file name get `_` in place of the bad characters. When two sounds or lines would get the same file name (for example the same line from two plugins in a flat folder), each one gets its archive or plugin name added, such as `NPCFAstrid_0001a2b3_MyMod.wav`.

When you export into a folder that already holds an export, a file is kept only when the earlier `list.csv` says it holds the same sound or line; if it holds another one (for example the same line from another plugin in a flat folder), the new one is written under its name with a short code added, such as `NPCFAstrid_0001a2b3_1f2e3d4c.wav`, and `--list` with `--out` shows those names before you export (and marks with `WILL FAIL` any item that has no free name left in that folder).

Without a `list.csv` there is nothing to check against, so use a new folder for each export when you turn the list off; when the list is on but an existing file isn't in it (the list was deleted, or couldn't be written last time), the file is kept and the log warns about it. A file or folder name that would start with `=`, `+`, `-` or `@` gets `_` in front.

An export into a folder that already has a `list.csv` from an earlier export of the same kind adds to that list: the earlier rows stay while their files are still there, and this export's rows replace the rows for the same files. So two searches exported into one folder give one list of both.

The columns of `list.csv` (UTF-8, so Excel shows accented names correctly):

* Sounds: **File** (the path below the export folder), **ID**, **Name**, **Group**, **Folder**, **Events**, **Bank**, **Source** and **Replaces**, as on the Sound Effects tab. **Group** is the group the sound is listed under on the left of the tab: the Wwise group (such as `WPN`) for a base game sound, and **Mod: <archive>** (such as `Mod: ShatteredSpace - Main01.ba2`) for a sound from a DLC, Creation or mod archive, so the rows split the same way `--group` does (`--group` takes the archive name without `Mod: `). With **Keep game folders**, the file itself still goes under its Wwise folder (such as `WPN\...`). Several events or replaced archives are joined with `; `.
* Voice lines: **File**, **Plugin**, **Voice Type**, **Game File** (the voice file's path in the game) and **Dialogue**.

A cell that starts with `=`, `+`, `-` or `@` (for example the dialogue "- Wait, don't go!") gets an apostrophe in front, so that Excel shows it as text instead of running it as a formula. A cell that already starts with an apostrophe gets a second one, so the tool reads the list back exactly. The **File** column never needs one, so it always matches the file on disk.

### From the command line
`starfieldvt-export.exe` is in the same folder as `StarfieldVT.exe` (`starfieldvt-export` on Linux). Open a command prompt there: open the folder in File Explorer, click the address bar at the top, type `cmd` and press Enter. Then run one of these:

```
starfieldvt-export sounds --out <folder> [--group WPN] [--search "lightsaber"] [options]
starfieldvt-export voices --out <folder> [--plugin Starfield.esm] [--voice-type NPCFSarahMorgan] [--search "good to see you"] [options]
```

The command (`sounds` or `voices`) comes first. The options:

| Option | What it does |
|---|---|
| `--out <folder>` | Where the `.wav` files go. The folder is created if it is missing. Not needed with `--list`. |
| `--group <name>` | Sounds only: one group (`AMB`, `WPN`, `MUS`, ...) or a mod's archive name (such as `"ShatteredSpace - Main01.ba2"`), as on the left of the Sound Effects tab. A group such as `WPN` holds only the base game's sounds; sounds from DLC, Creation and mod archives are in their archive's group. |
| `--plugin <file>` | Voices only: one plugin, such as `Starfield.esm`. |
| `--voice-type <id>` | Voices only: one voice type, such as `NPCFSarahMorgan`. |
| `--search <text>` | The same search as the tab's search or filter box. |
| `--flat` | Puts every file in one folder, like turning off **Keep game folders**. |
| `--no-csv` | Doesn't write `list.csv`. |
| `--list` | A dry run: prints how many files would be exported and the first 20, and writes nothing. |
| `--game <folder>` | The game's `Data` folder. Without it, the tool uses the path the window saved, then looks for a Steam or Xbox install. |
| `--verbose`, `-v` | Logs one line for every file. |
| `--log <file>` | Writes the log to `<file>` instead of the daily file. |
| `--help`, `-h` | Shows the help. |

A value can also follow an `=`, as in `--group=WPN`. An unknown group, plugin or voice type stops the command with the names that do exist; for a voice type, it also names the plugins that have it.

Example, writing the 84 microgun firing sounds (folder name changed):

```
> starfieldvt-export sounds --group WPN --search "microgun fire" --out "D:\Exports\microgun"
Reading sound archives in C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data...
Found 35491 sounds (6914 from mods, 97 inside banks, 1 without a name) in 19 sources in 1.2 seconds
Bulk export of 84 files to D:\Exports\microgun, layout game folders, list.csv on
Bulk export finished: 84 written, 0 kept, 0 failed, cancelled False, 0.1 s, list D:\Exports\microgun\list.csv
Exported 84 of 84 files in 0.1 s
List of files: D:\Exports\microgun\list.csv
```

Run it again and it prints `Exported 0 of 84 files; 84 already there (kept)`.

The `voices` command reads the window's voice line cache when it is up to date; otherwise it reads the plugins first, which takes about 30 seconds (as on the window's first start). The window and the command share the voice line search index. So when the command has to read the plugins again, or when you use `--search`, close the window first; otherwise the command stops with exit code 4 and says so. Only your own windows count: a window another Windows account has open uses that account's own index. A `voices` export without `--search` works with the window open, once the window has loaded its lines.

Press Ctrl+C once to stop after the current file; `list.csv` is still written. Press it a second time to stop at once.

The exit codes, for scripts:

| Code | Meaning |
|---|---|
| 0 | Every file was written or kept, or `--list` or `--help` finished. |
| 1 | Some files failed; the rest were written. The log names each failure. |
| 2 | The command line was wrong, or a group, plugin or voice type doesn't exist. |
| 3 | The game's `Data` folder wasn't found. Use `--game`. |
| 4 | The export couldn't run at all, for example because the export folder can't be made or the window is open when the search index is needed. The message says why. |
| 130 | Stopped with Ctrl+C. |

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

The tool always writes a log: `%LOCALAPPDATA%\StarfieldVT\logs\starfieldvt<date>.log` on Windows, `~/.local/share/StarfieldVT/logs/` on Linux. One file per day; the last 5 are kept. The log records the version, the Data folder and load order it read, which plugins it skipped and why, each archive read, and every playback or export error. `starfieldvt-export` writes its own log in the same folder, `starfieldvt-export<date>.log`.

For more detail, start the tool from a command prompt (or a shortcut) with these options:

| Option | What it does |
|---|---|
| `--verbose` (or `-v`) | Also logs every quest and dialogue topic parsed, every search, each step of converting a voice file or sound, and how many sounds each archive holds |
| `--log <file>` | Writes the log to `<file>` instead of the daily file, e.g. `--log C:\temp\starfieldvt.log` |

For example, in a command prompt or PowerShell window opened in the tool's folder: `.\StarfieldVT.exe --verbose --log C:\temp\starfieldvt.log` (Linux: `./StarfieldVT --verbose --log /tmp/starfieldvt.log`). Attach that file when you report a problem.

If a plugin's lines are missing, search the log for `skipped` or `Skipping`. `Plugins.txt` is shared by every Starfield install on the PC, so with more than one install (or a mod manager pointed at another folder) it can list plugins that aren't in the `Data` folder you chose. The tool skips those, and the log lists them.

If the Sound Effects tab shows fewer sounds than expected, search the log for `Found` (the line `Found N sounds (M from mods ...)`) and, with `--verbose`, for each archive's count; an archive or bank that can't be read is logged with `Could not read archive` or `Could not read bank`.

**Bulk exports need disk space and time.** All 35,491 sounds of a Steam install with Shattered Space and a few Creations take about 42 GB and about 10 minutes; one voice type with 5,000 lines takes about 2 GB and 2 minutes. Use `starfieldvt-export ... --list` first to see the count, and export to a drive with room to spare. If an export fails partway because the drive is full, free some space and run it again: the files already written are kept.

If some files of a bulk export fail, the window's status line and the command's summary give the count, and the log has one `Failed to export` line for each, naming the sound or line and the reason. `Could not write ...list.csv` usually means the file is open in Excel: close it and run the export again. With `--verbose`, the log has one `Wrote` or `Kept` line for every file.

If the voice lines look wrong or stale, use **File** → **Delete Cache**, or delete `%LOCALAPPDATA%\StarfieldVT\cache.json` and the `index` folder.

Please report issues at https://github.com/taosecurity/StarfieldVoiceTool/issues

## License
The source code in this repository is licensed under the [MIT License](LICENSE).

The release builds bundle third-party libraries, including [Mutagen](https://github.com/Mutagen-Modding/Mutagen), which is licensed under the GPL-3.0. The compiled program as a whole is therefore distributed under the GPL-3.0. [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) lists every bundled component with its license and source, and the full license texts are in [licenses](licenses). Both are included in every release zip.

## Changes in 0.10.0
* **Bulk export** on both tabs: **Export All Shown (N)**, **Export** with several rows selected (Ctrl+click and Shift+click now work in Voice Lines), the **Keep game folders** and **Write list.csv** check boxes, a progress bar with **Cancel**, and a question with a size estimate before exports of more than 1,000 files. See [Bulk export](#bulk-export). Thanks to MikeyMondavi for the idea.
* New command-line exporter `starfieldvt-export`, in the same folder as `StarfieldVT.exe`, with the `sounds` and `voices` commands and exit codes for scripts.
* At `--verbose`, the log has one line per exported file; the converter's step-by-step trace is no longer written.
* Because the version changed, the voice line cache rebuilds once on first start.

Every earlier version is in [CHANGELOG.md](CHANGELOG.md).
