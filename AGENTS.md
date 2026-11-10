# AGENTS.md: Starfield Voice Tool

Steps for an AI assistant helping someone use or build this tool. Every command below was run before the release it ships with.

## 1. What it is for

Starfield Voice Tool is a desktop app (Windows and Linux, x64) that lists every voiced dialogue line in Starfield and in the player's installed mods, grouped by plugin and voice type. The user can search the dialogue text, play a line, and export it as a `.wav`.

Use it when someone wants to:
- find or listen to a character's or voice type's lines;
- check which lines a mod adds and whether its voice files are found;
- get a voice line as a `.wav` file.

Don't use it to edit plugins, change voice files, or play the game: it only reads.

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
All tests pass. To also check voice file conversion on real files, set `STARFIELDVT_TEST_WEM_DIR` to a folder of `.wem` files first (game or mod voice files; never commit them).

## 5. Common tasks

| Task | How |
|---|---|
| Start the tool | `.\StarfieldVT.exe` (Linux: `./StarfieldVT`); from source: `dotnet run --project StarfieldVT -c Release` |
| Detailed log for a bug report | `.\StarfieldVT.exe --verbose --log "$env:TEMP\starfieldvt.log"` (from source: `dotnet run --project StarfieldVT -c Release -- --verbose --log "$env:TEMP\starfieldvt.log"`) |
| Choose another game folder | In the app: **File** → **Set Game Path** (the folder must contain `Starfield.esm`) |
| Mod voices packed outside `- Voices` archives (e.g. `- Main.ba2`) or as loose files | Read by default. **Options** → **Include voices from all mod archives and loose files** turns that off (reads only `- Voices` archives, slightly faster) |
| A plugin seems missing | Plugins are listed by file name (a Creation's file name can differ from its title: Watchtower is `kinggathcreations_spaceship.esm`), and only plugins with voiced lines are listed. Check the option above, then search the log for `skipped` |
| Rebuild the voice line list | In the app: **File** → **Delete Cache**; or delete `cache.json` and `index\` (section 6) |
| Find one line | Select a plugin or voice type on the left, then type in the filter box under the lines (Lucene syntax: `"exact phrase"`, `capt*`, `ship AND crew`) |
| Export a line | Select it, press **Export**, choose where to save the `.wav` |

Playing, exporting and choosing options are done in the window; there is no command-line mode for them.

## 6. Inputs, outputs, exit codes and logs

Inputs (read only):
- the game's `Data` folder: plugins (`.esm`, `.esp`, `.esl`), `.ba2` archives, and loose files under `Data\Sound\Voice`;
- the load order in `%LOCALAPPDATA%\Starfield\Plugins.txt`. Without one, every plugin in `Data` is read, each after its masters. Plugins listed there but not in the chosen `Data` folder are skipped and listed in the log.

Outputs:
- `.wav` files, only where the user chooses with **Export**;
- settings, cache and logs in `%LOCALAPPDATA%\StarfieldVT\` (Linux: `~/.local/share/StarfieldVT/`): `settings.json`, `cache.json`, `index\`, `logs\`;
- temporary conversion files in the system temp folder (`%TEMP%\StarfieldVT\`), deleted after each use and at start-up;
- on Linux only, if there is none: a `Plugins.txt` listing every plugin in `Data`, in `~/.local/share/Starfield/`.

Command-line options:

| Option | Effect |
|---|---|
| `--verbose`, `-v` | Debug-level log: every quest and dialogue topic parsed, every search, each voice conversion step |
| `--log <file>`, `--log=<file>` | Write the log to `<file>` instead of the daily file |

Unknown options are ignored and noted in the log.

Exit codes: 0 when the window is closed normally. An unexpected error closes the tool (the exit code is then the .NET runtime's crash code, not a tool-specific one) and is written to the log as `[FTL] Unhandled error`.

Log: `%LOCALAPPDATA%\StarfieldVT\logs\starfieldvt<yyyyMMdd>.log` (Linux: `~/.local/share/StarfieldVT/logs/`), one file per day, last 5 kept, or the `--log` file. Useful searches: `[ERR]`, `[WRN]`, `skipped`, `Skipping`, `ww2ogg:`.

## 7. Limits

- It never changes game files, plugins, archives, `Plugins.txt` (on Windows) or saves.
- It never uploads anything and needs no network connection.
- English voice files only; archives localized to other languages are skipped.
- Only enabled plugins in the load order (or, without `Plugins.txt`, every plugin in `Data`) are listed. Plugins whose masters are missing are skipped.
- Lines without a voice file (the player's lines, unvoiced text) are not listed.
- The window is required: it doesn't run headless or from a script.
- Exported audio is the game's or a mod's content: the user may only share it as that content's license allows.

Problems and requests: https://github.com/taosecurity/StarfieldVoiceTool/issues
