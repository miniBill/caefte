# CAEFTE #
C# and Elm for Tiny Executables.

## What's this? ##
This is a skeleton for building cross-platform (Windows, macOS, Linux), simple, tiny applications with C# and Elm. The resulting application will pop an icon in the notification area, and will be reachable inside the user's browser.

## Why C#? ##
The focus of CAEFTE is on tiny executables. Using C# means leveraging the massive .Net Framework, which is preinstalled on any Windows computer. 
This gives the developer access to a huge library with minimal footprint.

## Why Elm? ##
[Elm](https://elm-lang.org/) is a delightful language for reliable webapps. It's fast, the bundle size is small and the developer experience is awesome.

## Why should I use this ##
An hello world weights about 119k, 22kb if compressed and, on Windows, has no prerequisites.

To run the result on Linux/macOS you need to install [mono](https://www.mono-project.com/download/stable/).

## Requisites for compilation ##
If using Windows, install [WSL](https://docs.microsoft.com/it-it/windows/wsl/install-win10) and use that.

* [Parcel](https://parceljs.org/);
* `make`;
* [ImageMagick](https://imagemagick.org/script/download.php);
* [mono](https://www.mono-project.com/download/stable/);
* [pigz](https://zlib.net/pigz/) or [gzip](https://www.gzip.org/) (I *strongly* recommend installing `pigz`);
* [optipng](http://optipng.sourceforge.net/).

Except for parcel, the other packages should be available in any distro's repositories.

## Compile ##
```bash
make -j
```

The output will be put in the `out` directory.

## Run while developing ##
```bash
make run
```

This will start a live-reloading server (for the Elm part, the C# part still requires you to stop and restart the server).

## Similar projects ##
**Threepenny-gui**: https://wiki.haskell.org/Threepenny-gui - Haskell alternative
**Neutralinojs**: https://neutralino.js.org/ - C++ alternative

## TODO ##
* Integrate something like [this](https://github.com/dlech/Keebuntu) to fix the notification icon under mono/Linux.
* Investigate whether to use a tray icon vs a notification icon.
* Investigate whether to use a WebView to be more like Electron.
* Investigate detecting being run from the CLI and not popping an incon.
* Implement APIs exposing user types (expose as Elm's records).
* Implement APIs exposing progress [choose between (int, IEnumerable) f() or IEnumerable f(out int)]
* Implement APIs for reading data from body/url

PRs encouraged!
