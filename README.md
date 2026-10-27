![Workflow](https://github.com/taosecurity/StarfieldVoiceTool/actions/workflows/dotnet-desktop.yml/badge.svg?branch=main)  [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# Starfield Voice Tool
This tool is used to find voice lines by voice type in Starfield, filter them, play them and export them.

This is a fork of [yak3d/StarfieldVoiceTool](https://github.com/yak3d/StarfieldVoiceTool) with bug fixes; see [Changes](#changes-in-062).

## Requirements
* Starfield must be installed. Steam installs are detected automatically; otherwise you'll be asked for the game's `Data` folder (the one containing `Starfield.esm`).
* [ffmpeg](https://www.ffmpeg.org/), optional: if it isn't on `PATH`, the tool downloads a verified copy on first start.

## Installation
1. Download a [Release Zip](https://github.com/taosecurity/StarfieldVoiceTool/releases) or a [Nightly Zip](https://github.com/taosecurity/StarfieldVoiceTool/actions)
2. Extract to a folder of your choice

## Running
1. Ensure your load order in `plugins.txt` or your Mod Manager is set with the plugins you want the tool to scan
2. Run `StarfieldVT.exe` (`StarfieldVT` on Linux). The first start takes a while as it goes through your load order and finds the dialogue lines; progress is shown at the bottom of the window.
3. The results are cached in `%LOCALAPPDATA%\StarfieldVT\` (`cache.json` plus a search index in `index\`).
   * The cache is rebuilt automatically when the tool is updated, or when the game's plugins, archives, data folder or load order change.
   * **File** → **Delete Cache** forces a rebuild.
4. Select a plugin or voice type on the left. Double-click a line to play it, or select it and press **Export** to save it as a `.wav` wherever you choose.
5. The filter box under the lines searches the dialogue text. It supports [Lucene syntax](https://lucene.apache.org/core/4_8_0/queryparser/org/apache/lucene/queryparser/classic/package-summary.html#package_description) such as `"exact phrase"`, `capt*` and `ship AND crew`. With nothing selected, it searches every plugin.

### ffmpeg
ffmpeg is needed to convert `wem` audio to `wav` for playback and export. If it isn't on `PATH` or next to the executable, the tool downloads it once from https://github.com/ffbinaries/ffbinaries-prebuilt/releases (v5.1). The download is checked against a pinned SHA-256 hash, and only the `ffmpeg` binary is extracted, to `%LOCALAPPDATA%\StarfieldVT\ffmpeg\`.

## Troubleshooting
Log files are in `%LOCALAPPDATA%\StarfieldVT\logs`.

If the voice lines look wrong or stale, use **File** → **Delete Cache**, or delete `%LOCALAPPDATA%\StarfieldVT\cache.json` and the `index` folder.

Please report issues at https://github.com/taosecurity/StarfieldVoiceTool/issues

## License
The source code in this repository is licensed under the [MIT License](LICENSE).

The release builds bundle third-party libraries, including [Mutagen](https://github.com/Mutagen-Modding/Mutagen), which is licensed under the GPL-3.0. The compiled program as a whole is therefore distributed under the GPL-3.0. [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) lists every bundled component with its license and source, and the full license texts are in [licenses](licenses). Both are included in every release zip.

## Changes in 0.6.2
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
