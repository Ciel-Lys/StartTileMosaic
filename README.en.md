# StartTileMosaic

[简体中文](https://github.com/Ciel-Lys/StartTileMosaic#readme) · English

Create picture tiles for the native Windows 10 Start menu or the ExplorerPatcher tile menu on Windows 11. StartTileMosaic supports medium, wide, and large tiles, image mosaics, and timed image rotation. It is an independent project inspired by the Microsoft Store version of TileGenie.

## Download and run

Download the `portable.zip` asset from [GitHub Releases](https://github.com/Ciel-Lys/StartTileMosaic/releases/latest), extract it, and run `TileGenie.exe`.

- **Windows 10** uses the native tile menu. Tested on version 22H2.
- **Windows 11** requires [ExplorerPatcher](https://github.com/valinet/ExplorerPatcher) with its Windows 10-style Start menu enabled.

Developer Mode can stay off from the first launch. The first time you pin tiles, Windows asks for administrator approval to trust the signing certificate generated on your PC.

## Edit and pin tiles

1. Select **Add Images…** or drag images into the window. The editor supports PNG, JPEG, BMP, GIF, and TIFF. GIFs are imported as still images.
2. In **Grid Layout**, use the round buttons in the second toolbar row to add or remove columns and switch between medium and wide tiles. Use the buttons on the left to adjust rows. The default grid has two rows, with a wide column on the left and a medium column on the right. Click **Large Tiles** in the first row to switch the whole canvas to 310 × 310 tiles. Click it again to restore the previous layout.
3. Drag an image on the canvas to adjust its crop. Hold **Ctrl** and scroll to zoom. With multiple images, you can crop each one separately, reorder them, and choose a rotation interval. Select **Custom** to enter a whole number from 1 to 1,440 minutes. Windows schedules the rotation after pinning.
4. Under **Global binding**, enter an HTTP or HTTPS address or click **Choose File**. Windows opens the file using its file association. Each tile has one launch target. Select a tile to set its own target and click **Clear** to revert to the global binding. Click **Back to global binding** or press **Esc** to return to global scope. Setting a new global target replaces individual bindings. Unbound tiles open the TileGenie editor. Keep `TileGenie.exe` and any bound files in their original locations.
5. Select **Pin Tiles**, then arrange the tiles on Start to match the preview. The pictures use secondary tiles, so they do not add individual entries to **All apps**.

### Free layout

**Free Layout** opens with an empty canvas. Choose Medium, Wide, or Large from **Add Tile**. Hold the left button on a tile until the move cursor appears, then drag into an empty space. The canvas expands to fit. Right-click a tile to remove it. A quick left-click selects a tile, and dragging immediately adjusts the crop.

The canvas can extend beyond the width of a single Start group. TileGenie pins wider mosaics in groups from left to right, keeping each tile's image and size. Start spaces and arranges the groups based on the menu width. You can drag entire groups on Start to reposition them. Click **Grid Layout** to return to the grid. Both layouts stay available while the editor is open.

Use **语言 / Language** in the top-left menu to switch between Simplified Chinese and English. The editor remembers your choice. The menu also provides controls for rotating images, resetting the crop, setting tile spacing, and showing grid coordinates. To unpin a tile, right-click it on Start.

## Storage and installation

Source images and crop settings are saved under `%LOCALAPPDATA%\TileMosaic\Projects`. New tile packages are installed as signed MSIX files, and Windows manages their display assets. Tiles pinned by older versions that used Developer Mode still read their original project's `Package` folder.

On the first pin, the editor shows the certificate thumbprint and asks for consent. If you continue, Windows requests administrator approval to add the public certificate to the local computer's **Trusted People** store. Subsequent packages use the same certificate. The signing key is held by the current Windows account and is created as non-exportable. A system policy that blocks sideloading signed apps can still prevent installation.

Rotation uses Windows live tile notifications. Power-saving settings and background restrictions can affect when updates appear.

## Build

Run `.\build.ps1` from the project directory to produce `dist\TileGenie.exe`. The app uses the .NET Framework included with Windows. The Windows SDK is not required at runtime.

Use `--library <folder>` to select the tile data directory or `--image <path>` to import an image on launch.

## License

[MIT](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.md) for interface references and copyright notices.
