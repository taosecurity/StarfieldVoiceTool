# Privacy

Starfield Voice Tool runs on your own computer. It collects no data and sends nothing anywhere.

## What it does not do

- It makes no network connections and needs none.
- It has no telemetry, analytics, crash reporting or update checks.
- It never changes game files, plugins, archives, `Plugins.txt` or saves.
- It starts no other program.

## What it reads

- the game's `Data` folder: the plugins (`.esm`, `.esp`, `.esl`), the `.ba2` archives, and loose files under
  `Data\Sound\Voice` and `Data\Sound\Soundbanks`;
- the load order in `%LOCALAPPDATA%\Starfield\Plugins.txt`. Without it, every plugin in `Data` is read;
- its own settings, cache and search index (below).

## What it writes

- Settings, cache and logs in `%LOCALAPPDATA%\StarfieldVT\` (on Linux `~/.local/share/StarfieldVT/`): `settings.json`
  (window layout, the game folder, your options), `cache.json` and `index\` (the voice lines, with their dialogue
  text and plugin names, read from your own game files so the next start is fast), and `logs\`. They hold data from
  your own game files and your choices only, and stay on your computer. Use **File** > **Delete Cache** or delete the
  folder at any time; the tool rebuilds what it needs.
- The log: one file per day (`starfieldvt<date>.log` and, for the export command, `starfieldvt-export<date>.log`),
  the last 5 kept, or the file you name with `--log`. It lists the version, the Data folder, the load order, the
  archives it read, and every playback or export error. It contains folder paths, so it can include your Windows user
  name; read it before you share it.
- `.wav` files and `list.csv`, only where you choose with **Export**, **Export All Shown** or the `--out` folder of
  `starfieldvt-export`. They hold the game's and mods' own sound and dialogue; the game's content belongs to
  Bethesda and the mod authors, so check the terms before you share what you export.
- Temporary conversion files in the system temp folder (`%TEMP%\StarfieldVT\`), deleted after each use and at
  start-up.

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
