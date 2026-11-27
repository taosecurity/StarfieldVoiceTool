# Changelog

All versions of Starfield Voice Tool, newest first. Each version's section is also its release notes on GitHub.
## 0.10.1

### What's new

This is a fix release for four problems found in a review of 0.10.0. On Linux, the temporary folder is now private to your account. A saved game path that ends in a backslash no longer makes bulk exports and playback read the sound archives again for every line. Cancelling a rebuild can no longer leave dialogue search empty after a restart. And if the window starts while a `starfieldvt-export` run is using the search index, it now says so instead of failing.

### Example

```
> starfieldvt-export voices --voice-type NPCFAstrid --out "D:\Exports\one-group"
```

If the saved game folder ends in a backslash, 0.10.0 read all the sound archives again for every file; 0.10.1 reads them once. (The bug hunt replaces this command with its real output.)

### Changes

- A game folder saved with a trailing `\` or `/` now matches the shared archive index, so the archives are read once. Finding a file in the index is also faster.
- A cancelled rebuild of the dialogue tree no longer leaves a saved tree over an empty search index. The saved tree is deleted before the index is emptied.
- When the window starts while a command-line export holds the search index, it shows a message that says to wait or close the export, instead of failing.
- Linux: temporary conversion files go in a folder named `temp` inside your own app data folder, created so that only you can use it. They no longer go in the shared `/tmp/StarfieldVT`, where another account on the PC could plant links. Windows is unchanged.


## 0.10.0

### What's new

Starfield Voice Tool can now export many sounds or voice lines at once: a whole group, a mod's sounds, a voice type, or every result of a search. Both tabs have an **Export All Shown (N)** button, a progress bar and a **Cancel** button, and the files keep the game's folder layout, with a `list.csv` that says what each file is.

A new command-line program, `starfieldvt-export.exe`, does the same exports without the window, so they can run from a script. Thanks to MikeyMondavi for the idea.

### Example

```
> starfieldvt-export sounds --group WPN --search "microgun fire" --out "D:\Exports\microgun"
Reading sound archives in C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data...
Found 35491 sounds (6914 from mods, 97 inside banks, 1 without a name) in 19 sources in 1.2 seconds
Bulk export of 84 files to D:\Exports\microgun, layout game folders, list.csv on
Bulk export finished: 84 written, 0 kept, 0 failed, cancelled False, 0.1 s, list D:\Exports\microgun\list.csv
Exported 84 of 84 files in 0.1 s
List of files: D:\Exports\microgun\list.csv
```

### Changes

- **Export All Shown (N)** on the Voice Lines and Sound Effects tabs exports every row the table shows to a folder you choose.
- **Export** with several rows selected exports them the same way. In Voice Lines you can now select several lines with Ctrl+click and Shift+click; a double-click still plays the line you clicked.
- **Keep game folders** (on by default) writes sounds as `WPN\Hand\Microgun\<Name>_<ID>.wav` and voice lines as `<Plugin>\<Voice type>\<Voice type>_<file>.wav`. Off, every file goes into one folder.
- **Write list.csv** (on by default) writes `list.csv` in the export folder, with one row per file. For sounds, its **Group** column is the group on the left of the tab (**Mod: <archive>** for DLC, Creation and mod sounds), and **Wwise Group** is the sound's Wwise folder group. Both check boxes are remembered.
- A progress bar and a **Cancel** button show during an export. Cancel stops after the current file, and `list.csv` lists what was written.
- An export of more than 1,000 files first writes 50 of them, then asks before going on, with a size estimate and the drive's free space.
- Files that are already in the export folder are kept, so running the same export again only adds what is missing.
- No name from the game or a mod can point outside the export folder: a path that climbs up with `..` is refused on every system, including folders where letter case matters (Linux, and WSL folders on Windows).
- New `starfieldvt-export.exe` with the `sounds` and `voices` commands, a `--list` dry run, and exit codes for scripts. See [Bulk export](README.md#bulk-export) in the README.
- At `--verbose`, a bulk export logs one line per file. The converter's step-by-step trace moved below that level, so the log stays small.
- Because the version changed, the voice line cache rebuilds once on first start.

### Download

Download the Windows or Linux zip below, extract it, and run `StarfieldVT.exe` (`StarfieldVT` on Linux). `starfieldvt-export.exe` is in the same folder.

## 0.9.1
* The Sound Effects tab also lists sounds stored inside sound bank (`.bnk`) files, not only separate `.wem` files. In the base game that adds 97 sounds (the player character's sprint breathing, in the `VOC` group); mods that keep short sounds in their banks show them too. They play and export like any other sound, and the **Source** column names the bank they're in.
* Fixed: loading failed with "Mod was both a light and medium master" when any plugin in the load order had both the light (ESL) and the medium flag set in its header, and the message didn't say which plugin. That plugin is now skipped (along with any plugin that needs it), the log names it, and everything else loads. To use the plugin's lines, clear one of the two flags in xEdit or the Creation Kit.
* Because the version changed, the voice line cache rebuilds once on first start.

## 0.9.0
* A new **Sound Effects** tab lets you browse, search, play and export the game's and your mods' sound effects and music (about 35,000 sounds on a Steam install with Shattered Space and a few Creations). See [Sound effects](#sound-effects). The idea comes from [starfield-wwizard](https://github.com/yak3d/starfield-wwizard) by yak3d, the original author of this tool; this version is written from scratch from the file formats (no code was taken from it) and adds mod sounds, export, events and Linux support.
* Sounds stored as Wwise PCM (more than half of the sound effects) are now converted too. Voice lines are unchanged: they all use Wwise Vorbis, which goes through the same converter as before.
* A sound in a format the tool can't convert now gives a message naming the format, instead of a general conversion error.
* The bundled converter (our BnkExtractor fork) has a fix: Wwise Vorbis sounds with loop points, such as ambient loops and some music, failed with "Could not read full string!", because the converter wrote one of the loop comments in the Ogg header with the wrong length. Voice lines have no loop points and were never affected.
* Because the version changed, the voice line cache rebuilds once on first start.

## 0.8.1
* Fixed: the dialogue filter under the table ignored the voice type filter on the left once a plugin was selected. For example, with `sarahmorgan` on the left, a plugin selected and `weapons` in the dialogue filter, every voice type's "weapons" lines in that plugin were shown. The dialogue filter now searches only the lines the table is showing, so all three narrow together: plugin, voice type filter and dialogue text. The status line says what the table is limited to.
* The program has a new, original icon: a speech bubble with a sound wave on a starry background. It replaces the face icon inherited from the original tool, whose source was unknown. The icon is drawn from scratch by [`art/make_icon.py`](art/make_icon.py) (no game art) and is covered by this repository's MIT license.
* The window shows the icon on Linux too (before, only the Windows `.exe` had one).
* The plugin list on the left can be resized: drag the divider between it and the table. The tool remembers the width.
* Double-click the divider at the right edge of a column header (Voice Type, Filename, Dialogue) to fit that column to its contents, as in Windows Explorer. A double-click on a header no longer plays the selected line.
* A new **Help** menu has **Open Log Folder**, **Report a Problem…** (opens the GitHub issues page) and **About Starfield Voice Tool**. The About window shows the version, credits and license, links to the project and the third-party notices, the Data folder and settings folder in use, and a **Copy details** button for bug reports. The tool itself still never goes online; links open in your browser.
* Because the version changed, the voice line cache rebuilds once on first start.

## 0.8.0
* Fixed: with a `Plugins.txt` that lists plugins missing from the chosen `Data` folder (a second Starfield install, or a mod manager's load order for another folder), loading failed with "Failed to load voice types" and no lines were shown. Those plugins, and any plugin whose masters are missing, are now skipped and listed in the log.
* Fixed: mods built on a Creation (for example Terran Armada patches, which need `SFBGS050.esm`) made loading fail the same way, because the game loads Creations from its own catalog rather than `Plugins.txt`. Such masters are now loaded when they are in the `Data` folder, and the log lists them.
* **Include voices from all mod archives and loose files** is now on by default, and switched on once for existing settings (you can turn it off again). Without it, mods and Creations that pack voices in a ` - Main` archive, such as Astrid and Kinggath's Watchtower, showed no lines.
* Fixed: each played or exported line left the temporary `.wem` and the Vorbis codebook file open (a bug in the bundled BnkExtractor's ww2ogg port, now fixed in [taosecurity/BnkExtractor](https://github.com/taosecurity/BnkExtractor)). A failed conversion no longer leaves a half-written `.ogg` behind.
* New command-line options `--verbose` and `--log <file>` help with troubleshooting; see [Troubleshooting](#troubleshooting). Voice file conversion errors, which used to go only to a console window, are now in the log, as are unexpected errors that close the tool.
* The release zips include this README and [AGENTS.md](AGENTS.md).

## 0.7.0
* A new option, **Options** → **Include voices from all mod archives and loose files**, finds mod voices packed in archives without ` - Voices` in the name (for example `MyMod - Main.ba2`) and loose `.wem` files under `Data\Sound\Voice`. It is off by default. Turning it on or off rebuilds the cache.
* Without a `Plugins.txt` (mods copied into `Data` by hand, no mod manager), the tool now reads every plugin in the `Data` folder instead of only the game's built-in ones. Plugins are ordered after their masters; a plugin whose master is missing is skipped and noted in the log.

## 0.6.3
These Linux fixes were found by testing the Linux build for the first time:
* The Linux build no longer crashes at startup on systems without libicu, such as minimal Debian or WSL installs.
* Setting the game path no longer depends on a system folder picker. The dialog lets you type or paste the path, and **Browse…** uses the picker where one exists. Without a picker (no xdg-desktop-portal or GTK), the first-run setup used to wait forever.
* Native Steam and Proton installs of Starfield are detected automatically on Linux.
* Playback works with the system VLC from the normal runtime package (`libvlc5`). It previously looked for `libvlc.so`, which only the developer package provides. If VLC is missing, the error now says what to install.
* Every line plays reliably, short ones included. A long-lived VLC instance went silent after the first sound on some audio setups (seen on WSLg), and short lines could be cut off when playback ended. This affected Windows too, with high-latency audio such as Bluetooth.
* A new [Linux](#linux) section lists the packages to install.

## 0.6.2
* **ffmpeg is no longer needed or downloaded.** Voice files are now decoded inside the app ([NVorbis](https://github.com/NVorbis/NVorbis)), which removes the first-start download of a third-party ffmpeg build. The output was checked against ffmpeg's on 300 voice lines from every plugin, and it was identical. A copy downloaded by an earlier version in `%LOCALAPPDATA%\StarfieldVT\ffmpeg` is no longer used and can be deleted.
* Temporary audio files are deleted right after each conversion (a file handle left open by the ww2ogg converter previously kept them until the next start).
* Release zips include `LICENSE.txt`, `THIRD-PARTY-NOTICES.md` and a `licenses` folder with the license texts of all bundled components, with links to the exact source of the GPL and LGPL ones.
* An unused icon library (Projektanker.Icons.Avalonia.FontAwesome) is removed.

## 0.6.1
* Release zips extract with the app at the top level, instead of inside an `artifacts\<name>` folder.
* The Linux `StarfieldVT` binary in the release zip is executable again; previously it needed `chmod +x`.
* The build pipeline uses current GitHub Actions versions.

## 0.6.0
* **Export** asks where to save the `.wav`. It used to write into the game's `Data\sound\voice` folder without asking, and crashed if that failed.
* The game path is set up before loading starts. Previously the first run failed if the game wasn't auto-detected, and changing the path didn't reload anything.
* The progress bar tracks the real quest count per plugin. It used to say "Loaded" after 100 quests, and stayed visible forever when the cache was used.
* A race that could cut off a new line when the previous one finished playing is fixed.
* Typing incomplete search syntax (e.g. a lone `"` or `(`) no longer breaks the filter.
* Voice archives are chosen by name pattern: English (`_en`) or language-neutral archives only. Before, a mod named like another plugin, or containing "en", could pull in the wrong archives or languages. Voice folders for `.esl` plugins no longer abort loading.
* Loading is much faster: the voice archives are indexed once instead of once per plugin, lines are no longer copied on every insert, and selecting a large plugin no longer rebuilds lists repeatedly.
* Playback and export run in the background, so the window doesn't freeze. Temporary audio files are cleaned up.
* The cache is invalidated when the game files or load order change, not only on a tool update. It's also written atomically.
* Errors (load failures, playback/export problems, ffmpeg download failures) are shown in the window, not just in the log.
* Dead code is removed, and unit tests are added for the core parsing, cache and search logic.
