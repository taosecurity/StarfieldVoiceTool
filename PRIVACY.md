# Privacy

Starfield Voice Tool runs on your own computer. It collects no data and sends nothing anywhere.

## What it does not do

- It makes no network connections of its own and needs none.
- It has no telemetry, analytics, crash reporting or update checks.
- It never changes game files, plugins, archives, `Plugins.txt` or saves.
- It starts no other program, except that the links in **Help** and **About** (such as **Report a Problem**) open
  in your web browser, and **Open Log Folder** and the notices file open in your file manager or text viewer. That
  happens only when you click them.

## What it reads

- It reads the game's `Data` folder: the plugins (`.esm`, `.esp`, `.esl`), the `.ba2` archives, and loose files
  under `Data\Sound\Voice` and `Data\Sound\Soundbanks`.
- It reads the load order in `%LOCALAPPDATA%\Starfield\Plugins.txt`. Without it, every plugin in `Data` is read.
- It reads its own settings, cache and search index (below).

## What it writes

- Settings, cache and logs go in `%LOCALAPPDATA%\StarfieldVT\` (on Linux `~/.local/share/StarfieldVT/`). They are
  `settings.json` (window layout, the game folder, your options), `cache.json` and `index\` (the voice lines, with
  their dialogue text and plugin names, read from your own game files so the next start is fast), and `logs\`. They
  hold data from your own game files and your choices only, and stay on your computer. Use **File** > **Delete
  Cache** or delete the folder at any time; the tool rebuilds what it needs.
- The log is one file per day (`starfieldvt<date>.log` and, for the export command, `starfieldvt-export<date>.log`),
  with the last 5 kept, or the file you name with `--log`. It lists the version, the Data folder, the load order, the
  archives it read, and every playback or export error. It contains folder paths, so it can include your Windows user
  name; read it before you share it.
- `.wav` files and `list.csv` are written only where you choose with **Export**, **Export All Shown** or the `--out`
  folder of `starfieldvt-export`. They hold the game's and mods' own sound and dialogue; the game's content belongs
  to Bethesda and the mod authors, so check the terms before you share what you export.
- Temporary conversion files go in `%TEMP%\StarfieldVT\` on Windows, and on Linux in
  `~/.local/share/StarfieldVT/temp/`, which the tool creates so that only you can use it. They are deleted after
  each use and at start-up.

It plays sound through your computer's audio output, and nothing else leaves the program.

## Personal data

Starfield Voice Tool handles none. Its logs show file names and folder paths, and its cache holds the game's dialogue
text.

## Downloading it

Downloading Starfield Voice Tool from GitHub is covered by GitHub's own
[privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement).
The author receives only the public download counts GitHub shows.

## Questions

Open an issue at https://github.com/taosecurity/StarfieldVoiceTool/issues.
