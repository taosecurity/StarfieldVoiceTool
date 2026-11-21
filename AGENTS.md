# AGENTS.md: Starfield Voice Tool

Steps for an AI assistant helping someone use or build this tool. Every command below was run before the release it ships with.

## 1. What it is for

Starfield Voice Tool is a desktop app (Windows and Linux, x64) that lists every voiced dialogue line in Starfield and in the player's installed mods, grouped by plugin and voice type. The user can search the dialogue text, play a line, and export it as a `.wav`. A second tab, **Sound Effects**, lists every Wwise sound (sound effects, ambience, music) from the game and from mods, by name, Wwise ID, folder and event; each can be played, exported as a `.wav`, or have its ID or event names copied. Since 0.10.0, both tabs export many lines or sounds at once (bulk export), and the command-line program `starfieldvt-export` does the same bulk exports without the window. This file describes version 0.10.0.

Use it when someone wants to:
- find or listen to a character's or voice type's lines;
- check which lines a mod adds and whether its voice files are found;
- get a voice line as a `.wav` file, or all of a voice type's or plugin's lines at once;
- save a whole group of sounds (or every sound) as `.wav` files with a `list.csv` index, from the window or a script;
- find a sound effect's Wwise ID or event name (for example to replace it in a mod), hear it, or save it as a `.wav`;
- see which sounds a mod adds or replaces.

Don't use it to edit plugins, change voice or sound files, or play the game: it only reads.

## 2. Prerequisites

- Starfield installed (Steam, Game Pass/Xbox app, or any folder that has `Data\Starfield.esm`).
- To run a release: nothing else on Windows 10/11 x64. On Linux x64 (including WSL with WSLg), the VLC and X11 libraries. On Debian or Ubuntu:
  ```
  sudo apt install libvlc5 vlc-plugin-base libfontconfig1 libice6 libsm6 libx11-6 libx11-xcb1
  ```
- To build from source: the .NET SDK 10.0 (tested with 10.0.403) and git.

## 3. Install

From a release:
1. Download `starfieldvt-windows-<commit>-v<version>.zip` (or `starfieldvt-linux-...`) from https://github.com/taosecurity/StarfieldVoiceTool/releases.
2. Extract it to any folder. There is no installer.

From source (the BnkExtractor submodule is required):
```
git clone --recursive https://github.com/taosecurity/StarfieldVoiceTool.git
cd StarfieldVoiceTool
dotnet build StarfieldVT.sln -c Release
```

## 4. Prove the install works

Release (Windows PowerShell, in the extracted folder; wait until the window shows lines, then close it):
```
.\StarfieldVT.exe --log "$env:TEMP\starfieldvt-check.log"
```
(In cmd: `.\StarfieldVT.exe --log "%TEMP%\starfieldvt-check.log"`. On Linux: `./StarfieldVT --log /tmp/starfieldvt-check.log`.)

The first line of that log is `Starfield Voice Tool <version> starting on ...`, and on the first start (or after **File** → **Delete Cache**) loading ends with a line `Found <N> lines in <S> seconds`. That first load takes about 20 seconds to a few minutes; later starts log `Loading cache from ...` instead and are quick. Lines with `[ERR]` mean something failed; section 6 lists what to search for.

Source:
```
dotnet test StarfieldVT.Core.Tests -c Release
```
All tests pass.

The command-line exporter (release folder, or `dotnet run --project StarfieldVT.Export -c Release -- <arguments>` from source):
```
.\starfieldvt-export.exe sounds --list --group WPN --search microgun
```
It prints `Found <N> sounds (...) in <M> sources in <S> seconds`, then `100 sounds would be exported (game folders, list.csv on):` and the first 20 paths, and exits 0. It writes nothing. Exit code 3 means it found no game `Data` folder: add `--game "<path to Data>"`.

To also check conversion on real files, set `STARFIELDVT_TEST_WEM_DIR` to a folder of `.wem` files first (game or mod voice or sound files, Vorbis or PCM; never commit them). To also list the sounds of a real install, set `STARFIELDVT_TEST_DATA_DIR` to its `Data` folder (read only); the test writes a summary to `%TEMP%\svt-real-index.txt`.

## 5. Common tasks

| Task | How |
|---|---|
| Start the tool | `.\StarfieldVT.exe` (Linux: `./StarfieldVT`); from source: `dotnet run --project StarfieldVT -c Release` |
| Detailed log for a bug report | `.\StarfieldVT.exe --verbose --log "$env:TEMP\starfieldvt.log"` (from source: `dotnet run --project StarfieldVT -c Release -- --verbose --log "$env:TEMP\starfieldvt.log"`) |
| Choose another game folder | In the app: **File** → **Set Game Path** (the folder must contain `Starfield.esm`) |
| Mod voices packed outside `- Voices` archives (e.g. `- Main.ba2`) or as loose files | Read by default. **Options** → **Include voices from all mod archives and loose files** turns that off (reads only `- Voices` archives, slightly faster) |
| A plugin seems missing | Plugins are listed by file name (a Creation's file name can differ from its title: Watchtower is `kinggathcreations_spaceship.esm`), and only plugins with voiced lines are listed. Check the option above, then search the log for `skipped` |
| Rebuild the voice line list | In the app: **File** → **Delete Cache**; or delete `cache.json` and `index\` (section 6) |
| Find the log, or details for a bug report | In the app: **Help** → **Open Log Folder**; **Help** → **About Starfield Voice Tool** → **Copy details** (version, OS, Data folder, log location) |
| Report a problem | In the app: **Help** → **Report a Problem…** (opens https://github.com/taosecurity/StarfieldVoiceTool/issues) |
| Resize the plugin list or fit a column | Drag the divider between the list and the table (the width is remembered in `settings.json`); double-click the right edge of a column header to fit it |
| Find one line | Select a plugin or voice type on the left, then type in the filter box under the lines (Lucene syntax: `"exact phrase"`, `capt*`, `ship AND crew`) |
| Export a line | Select it, press **Export**, choose where to save the `.wav` |
| Export many lines or sounds in the window | Select several (Ctrl+click, Shift+click) and press **Export**, or press **Export All Shown (N)** for every row the table shows; choose a folder. **Keep game folders** and **Write list.csv** (both on by default, shared by both tabs) set the layout and the index. **Cancel** stops after the current file. Over 1,000 files, the tool writes 50, then asks with a size estimate |
| List what a bulk export would write | `.\starfieldvt-export.exe sounds --list --group WPN` or `.\starfieldvt-export.exe voices --list --plugin Starfield.esm --voice-type NPCFSarahMorgan` |
| Export a group of sounds | `.\starfieldvt-export.exe sounds --group MUS --out "D:\Exports\music"` |
| Export every sound (about 42 GB, about 10 minutes) | `.\starfieldvt-export.exe sounds --out "D:\Exports\sounds" --log "$env:TEMP\svt-export.log"` |
| Export a voice type's lines | `.\starfieldvt-export.exe voices --plugin Starfield.esm --voice-type NPCFSarahMorgan --out "D:\Exports\sarah"` |
| Export the result of a search | Add `--search "<text>"` to `sounds` or `voices` (the same search as the tab's box) |
| One flat folder, or no `list.csv` | Add `--flat` or `--no-csv` |
| Finish an export that failed or was stopped | Run the same command again: files already there are kept, and only the missing ones are written |
| Find a sound effect | **Sound Effects** tab (listed the first time it is opened, a few seconds); pick a group on the left (`WPN`, `MUS`, ... or **Mod: <archive>**), type in the search box: words match name, folder, bank or event; a number matches a whole Wwise ID |
| Play or export sound effects | Double-click or ▶ plays; **Export** saves one `.wav`, or with several selected asks for a folder and writes `<Name>_<ID>.wav` each (existing files are kept) |
| Get a sound's Wwise ID or events | Select the sounds, press **Copy ID** or **Copy Events** (one per line on the clipboard) |

Playing, single-file exports and choosing options are done in the window. Bulk exports can also run from `starfieldvt-export`; the window itself has no command-line mode for them.

## 6. Inputs, outputs, exit codes and logs

Inputs (read only):
- the game's `Data` folder: plugins (`.esm`, `.esp`, `.esl`), `.ba2` archives, and loose files under `Data\Sound\Voice`;
- for the Sound Effects tab: the `.wem`, `.bnk` and bank `.json` files directly in `sound\soundbanks` of every archive (in load order) and of `Data\Sound\Soundbanks`; the later source wins for the same ID;
- the load order in `%LOCALAPPDATA%\Starfield\Plugins.txt`. Without one, every plugin in `Data` is read, each after its masters. Plugins listed there but not in the chosen `Data` folder are skipped and listed in the log.

Outputs:
- `.wav` files, only where the user chooses with **Export** or **Export All Shown**, or in the `--out` folder of `starfieldvt-export`. A bulk export writes each file as `<name>.wav.part` and renames it when complete, keeps files that already exist, and (unless turned off) writes `list.csv` in the export folder. Sound columns: File, ID, Name, Group, Folder, Events, Bank, Source, Replaces. Voice columns: File, Plugin, Voice Type, Game File, Dialogue. UTF-8 with a BOM;
- settings, cache and logs in `%LOCALAPPDATA%\StarfieldVT\` (Linux: `~/.local/share/StarfieldVT/`): `settings.json`, `cache.json`, `index\`, `logs\`;
- temporary conversion files in the system temp folder (`%TEMP%\StarfieldVT\`), deleted after each use and at start-up;
- on Linux only, if there is none: a `Plugins.txt` listing every plugin in `Data`, in `~/.local/share/Starfield/`.

Command-line options:

| Option | Effect |
|---|---|
| `--verbose`, `-v` | Debug-level log: every quest and dialogue topic parsed, every search, each voice or sound conversion step (with its codec), each archive's sound count |
| `--log <file>`, `--log=<file>` | Write the log to `<file>` instead of the daily file |

Unknown options are ignored and noted in the log.

`starfieldvt-export` options: the command `sounds` or `voices` first, then `--out <folder>` (not needed with `--list`), `--group <name>` (sounds: a base-game group such as `WPN`, which leaves out DLC, Creation and mod sounds, or an archive name such as `"ShatteredSpace - Main01.ba2"` for those), `--plugin <file>` and `--voice-type <id>` (voices), `--search <text>`, `--flat`, `--no-csv`, `--list`, `--game <Data folder>`, `--verbose`/`-v`, `--log <file>`, `--help`/`-h`. Values may follow as the next argument or after `=`. Unknown options are errors (exit 2). Without `--game`, it uses the game path the window saved, then the Steam or Xbox lookup, and it never saves settings. `voices` uses the window's voice cache when it is current, otherwise it reads the plugins (about 30 seconds) and writes that shared cache; don't run it while the window is loading.

`starfieldvt-export` exit codes:

| Code | Meaning |
|---|---|
| 0 | Every file written or kept, or `--list`/`--help` done (also when nothing matched; a warning says so) |
| 1 | Some files failed (each has a log line); the rest were written |
| 2 | Bad command line, or an unknown group, plugin or voice type (the message lists the valid names) |
| 3 | No game `Data` folder found (no `Starfield.esm`); use `--game` |
| 130 | Stopped with Ctrl+C (the current file finishes and `list.csv` is written); a second Ctrl+C ends it at once |

Window exit codes: 0 when the window is closed normally. An unexpected error closes the tool (the exit code is then the .NET runtime's crash code, not a tool-specific one) and is written to the log as `[FTL] Unhandled error`.

Log: `%LOCALAPPDATA%\StarfieldVT\logs\starfieldvt<yyyyMMdd>.log` (Linux: `~/.local/share/StarfieldVT/logs/`), one file per day, last 5 kept, or the `--log` file. Useful searches: `[ERR]`, `[WRN]`, `skipped`, `Skipping`, `ww2ogg:`; for sound effects, `Found` (the line `Found N sounds (M from mods, E inside banks, K without a name) in S sources`), `Could not read bank`, `Could not read archive`, `Playing sound`, `Exporting sound`, `couldn't be converted`.

`starfieldvt-export` logs to the same daily file (or `--log`). Bulk exports, from the window or the command, log these lines:
- Information, once per export: `Bulk export of <N> files to <folder>, layout <game folders|flat>, list.csv <on|off>`, then the summary `Bulk export finished: <W> written, <K> kept, <F> failed, cancelled <True|False>, <S> s, list <path>`;
- Debug (`--verbose`), one per file: `Wrote <path> (<what>)` or `Kept <path> (already there)`;
- Error, one per failure: `Failed to export <sound or line> to <path>`, with the reason; `Could not write <list.csv>` when the list can't be written (often open in Excel);
- Warning (command only): `No <sounds|voice lines> match these options, so nothing was exported`;
- Information (window only, over 1,000 files): `Asking before a big export`, and `Big export not confirmed` when the user says no.
The converter's own step-by-step trace is below `--verbose` and isn't written.

## 7. Limits

- It never changes game files, plugins, archives, `Plugins.txt` (on Windows) or saves.
- It never uploads anything and needs no network connection.
- English voice files only; archives localized to other languages are skipped.
- Only enabled plugins in the load order (or, without `Plugins.txt`, every plugin in `Data`) are listed. Plugins whose masters are missing are skipped, and so are plugins with both the light and the medium flag in their header (the log names them; clearing one flag in xEdit or the Creation Kit fixes it).
- Lines without a voice file (the player's lines, unvoiced text) are not listed.
- Sound effects: streamed `.wem` files in `sound\soundbanks` are listed, and sounds stored inside the `.bnk` banks the game uses (the last copy of each bank wins). Inside banks, prefetch copies of streamed sounds and Wwise plugin data (reverb impulse responses, JSON `Path` starting `Plugins\`) are skipped. Wwise PCM and Wwise Vorbis are converted; any other codec is refused with a message naming it. The sound list is not cached: it is read again each time the tool starts and the tab is opened.
- The window is required for browsing and playing: it doesn't run headless, and its clicks can't be scripted. Only bulk exports have a command-line program (`starfieldvt-export`).
- Bulk exports read the game's files only and never change them; they write only below the chosen export folder. They need free disk space: all 35,491 sounds of a Steam install take about 42 GB and about 10 minutes. Use `--list` first. Exports run one file at a time, and one export at a time in the window.
- Exported audio is the game's or a mod's content: the user may only share it as that content's license allows.

Problems and requests: https://github.com/taosecurity/StarfieldVoiceTool/issues
