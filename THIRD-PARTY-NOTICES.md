# Third-party notices

Starfield Voice Tool's own source code is licensed under the MIT License (see `LICENSE`).

The release builds also contain the third-party components listed below. Some of them are licensed
under the **GNU General Public License v3.0** (Mutagen and its dependencies). **The compiled program
as a whole is therefore distributed under the terms of the GPL-3.0.** The full license texts are in
the `licenses` folder that accompanies this file.

Starfield is a trademark of ZeniMax Media Inc. / Bethesda Softworks. This tool is not affiliated with
or endorsed by them, and it contains no game files.

## Source code

* **This program:** https://github.com/taosecurity/StarfieldVoiceTool. Each release is built by
  GitHub Actions from the source at its version tag (e.g. `v0.6.2`), including the BnkExtractor
  submodule at the commit recorded in that tag.
* **GPL and LGPL components:** the exact sources are linked in the tables below.

If any of those links stops working, open an issue at
https://github.com/taosecurity/StarfieldVoiceTool/issues and the corresponding source code will be
provided. This offer is valid for at least three years after the last release that included the
component.

## GPL-3.0 components

License text: `licenses/GPL-3.0.txt`

| Component | Version | Copyright | Source |
|---|---|---|---|
| Mutagen.Bethesda (Core, Kernel, Starfield, Skyrim, Fallout4, Oblivion) | 0.49.0-alpha.44 | Noggog and Mutagen contributors | https://github.com/Mutagen-Modding/Mutagen/tree/d7ec5af4ea961361f833290095700cf9126ca104 |
| Loqui | 2.70.0-alpha.2 | Noggog | https://github.com/Noggog/Loqui/tree/96558f70a6a4dc118687dc0676800534ae0287a9 |
| Noggog.CSharpExt | 2.70.0-alpha.12 | Noggog | https://github.com/Noggog/CSharpExt/tree/fc8cb7efff1f8474dc33eae2d47f9452760f1ee3 |
| GameFinder (Common, RegistryUtils, StoreHandlers.GOG/Steam/Xbox, Wine) | 4.3.3 | erri120 | https://github.com/erri120/GameFinder/tree/v4.3.3 |
| NexusMods.Paths | 0.10.0 | Nexus Mods | https://github.com/Nexus-Mods/NexusMods.Paths/tree/v0.10.0 |
| Reloaded.Memory | 9.4.1 | Sewer56 (some files MIT, from the Microsoft Community Toolkit; see `licenses/Reloaded.Memory-LICENSE.md`) | https://github.com/Reloaded-Project/Reloaded.Memory/tree/9.4.1 |

## LGPL components (Windows build only)

License texts: `licenses/LGPL-2.1.txt`, `licenses/GPL-2.0.txt`

| Component | Version | License | Source |
|---|---|---|---|
| LibVLCSharp | 3.9.3 | LGPL-2.1-or-later, Copyright VideoLAN and contributors | https://github.com/videolan/libvlcsharp/tree/3.9.3 |
| LibVLC (`libvlc` folder: libvlc, libvlccore and plugins) | 3.0.23 | LGPL-2.1-or-later; some plugins GPL-2.0-or-later. Copyright VideoLAN and the VLC authors | https://download.videolan.org/pub/videolan/vlc/3.0.23/ (third-party libraries in the plugins: https://download.videolan.org/pub/videolan/contrib/) |

LibVLC is loaded as separate DLLs from the `libvlc` folder, so you may replace them with your own
modified build of the same LibVLC version.

## Apache-2.0 components

License text: `licenses/Apache-2.0.txt`

| Component | Version | Copyright / notice |
|---|---|---|
| Lucene.Net (+ Analysis.Common, Queries, QueryParser, Sandbox) | 4.8.0-beta00017 | The Apache Software Foundation; see `licenses/Lucene.Net-NOTICE.txt` and `licenses/Lucene.Net-LICENSE.txt` (includes licenses of code Lucene.Net incorporates) |
| J2N | 2.1.0 | Copyright 2019 J2N |
| Serilog, Serilog.Sinks.Console, Serilog.Sinks.File | 4.0.1, 6.0.0, 6.0.0 | Copyright © 2013-24 Serilog Contributors |

## BSD and ISC components

| Component | License | Copyright |
|---|---|---|
| ww2ogg (C# port inside BnkExtractor, including its embedded Vorbis codebooks) | BSD-3-Clause, `licenses/ww2ogg-BSD-3-Clause.txt` | Copyright (c) 2002, Xiph.org Foundation; Copyright (c) 2009-2016, Adam Gashlin |
| revorb (C# port inside BnkExtractor) | ISC-style, `licenses/revorb-ISC.txt` | Copyright (c) 2008, Jiri Hruska |
| ANGLE (`av_libglesv2.dll`, Windows build, via Avalonia.Angle.Windows.Natives 2.1.22045.20230930) | BSD-3-Clause, `licenses/ANGLE-BSD-3-Clause.txt` | Copyright The ANGLE Project Authors |
| Skia and other native code in libSkiaSharp | see `licenses/SkiaSharp-THIRD-PARTY-NOTICES.txt` | Google Inc. and others |
| HarfBuzz and other native code in libHarfBuzzSharp | see `licenses/HarfBuzzSharp-THIRD-PARTY-NOTICES.txt` | HarfBuzz authors and others |

## MIT components

License text: `licenses/MIT.txt`, applied with each copyright notice below.

| Component | Version | Copyright |
|---|---|---|
| Starfield Voice Tool (original) | | Copyright (c) 2024 yak |
| BnkExtractor | submodule | Copyright (c) 2021 ds5678 (`licenses/BnkExtractor-MIT.txt`); its bnk parser is based on bnkextr, which is public domain or MIT |
| .NET runtime (bundled in the self-contained build) | 10.0 | Copyright (c) .NET Foundation and Contributors; see `licenses/dotnet-runtime-LICENSE.txt` and `licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt` |
| Microsoft.Bcl.AsyncInterfaces, Microsoft.Extensions.* (Configuration.Abstractions, DependencyInjection.Abstractions, Logging.Abstractions, Primitives), System.Linq.Async, System.Reactive | various | Copyright (c) .NET Foundation and Contributors |
| Avalonia (+ Controls.DataGrid, Desktop, FreeDesktop, Native, Remote.Protocol, Skia, Themes.Fluent, Win32, X11) | 11.2.7 | Copyright (c) AvaloniaUI OÜ |
| SkiaSharp, HarfBuzzSharp (managed bindings) | 2.88.9, 7.3.0.3 | Copyright (c) 2015-2016 Xamarin, Inc.; Copyright (c) 2017-2018 Microsoft Corporation |
| MicroCom.Runtime | 0.11.0 | Copyright (c) 2021 Nikita Tsukanov |
| Tmds.DBus.Protocol | 0.92.0 | Copyright 2016 Tom Deseyn; Copyright 2010 Other Contributors |
| DynamicData | 9.0.4 | Copyright (c) Roland Pheasant 2011-2022 |
| FFMpegCore | 5.1.0 | Copyright (c) 2023 Vlad Jerca |
| Instances | 3.0.0 | Copyright (c) 2020 Malte Rosenbjerg |
| FluentResults | 3.15.2 | Copyright (c) 2021 Michael Altmann |
| OggVorbisSharp | 1.0.1 | Copyright (c) the OggVorbisSharp authors (https://github.com/SamboyCoding/OggVorbisSharp) |
| SharpZipLib | 1.4.2 | Copyright © 2000-2018 SharpZipLib Contributors |
| System.IO.Abstractions, TestableIO.System.IO.Abstractions(.Wrappers) | 21.1.3 | Copyright (c) Tatham Oddie and Contributors |
| ValveKeyValue | 0.10.0.360 | Copyright (c) 2016 ValveKeyValue Contributors |
| K4os.Compression.LZ4, K4os.Compression.LZ4.Streams | 1.3.8 | Copyright (c) 2017 Milosz Krajewski |
| K4os.Hash.xxHash | 1.0.8 | Copyright (c) 2017 Milosz Krajewski |
| ini-parser-netstandard | 2.5.2 | Copyright (c) 2008 Ricardo Amores Hernández |
| OneOf | 3.0.271 | Copyright (c) 2016 Harry McIntyre |

## Not included: ffmpeg

ffmpeg is **not** shipped with this program. If it isn't already installed, the program downloads it
on first start from https://github.com/ffbinaries/ffbinaries-prebuilt/releases (v5.1), verifies it
against a pinned SHA-256 hash and stores it in `%LOCALAPPDATA%\StarfieldVT\ffmpeg`. That build is
provided by the ffbinaries project under ffmpeg's own license (GPL); see https://ffmpeg.org/legal.html.
