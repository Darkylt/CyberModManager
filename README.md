# Cyber Mod Manager

A small, free mod manager for Cyberpunk 2077.

## Using it

1. Run `CyberModManager.exe`.
2. The game folder is found automatically for Steam, GOG and Epic. If it isn't, click **Browse...** and pick the folder that contains `bin`, `archive`, `r6`.
3. Drag `.zip` / `.7z` / `.rar` mod files onto the window, or click **Add mods...**.
4. Select mods, then click **Install**. Select mods, then click **Uninstall** to remove them.

**Updating a mod:** select it, then drop the new version onto the **Update this mod** box in the details pane, or click the box to pick the file (also under right-click -> **Update...**). After you confirm, the old version is taken out of the game and the new one installed in its place; the name and category are kept. The new file is checked first, so a broken download never removes the working version. Dropping a file with the same name as an existing mod onto the list also offers to replace it.

**Organizing:** type in the search box (**Ctrl+F**) to filter by name, file name or category, and pick a category from the dropdown to show only that group. Right-click mods -> **Category** to put them in one (pick a suggestion, make a new one, or rename an existing one). Click a column header to sort. While a search or category filter is active, **Install shown** / **Uninstall shown** act on just the mods in view.

Other actions: **F2** or double-click renames a mod, **Delete** removes it from the list, and right-click opens a menu.

## What it does for you

- **Copies each archive into its own data folder** (`ModManagerData\` next to the exe), so you can delete your downloads.
- **Records every file it installs**, so uninstalling removes only those files, plus any folders it created that are now empty.
- **Handles overlapping mods:** if a mod overwrites a file from the game or from another mod, the old file is backed up and put back when you uninstall. This works in any uninstall order.
- **Fixes common packaging mistakes:** removes an extra wrapper folder (`MyMod v1.2\archive\...`), puts loose `.archive` files in `archive\pc\mod`, and leaves readmes and screenshots out of the game folder.
- **Password protected archives:** it asks for the password once and remembers it.
- **If an install fails partway** (for example because the game is running and a file is locked), it's rolled back automatically.

## Updating the manager

Close it, replace `CyberModManager.exe` with the new one, and start it again. Everything (mod list, categories, stored archives, backups of overwritten files) lives in the `ModManagerData` folder next to the exe, not inside the exe, so it carries over. Each start also keeps a copy of the previous `state.json` as `state.json.bak`, which is used automatically if the main file is ever damaged.

> Don't delete or edit the `ModManagerData` folder while mods are installed. It's how the app knows what to remove.
> To move the manager to another place, move the exe **and** that folder together.

## Building

Requires the .NET 10 SDK.

```powershell
# run from source
dotnet run --project src

# tests
dotnet run --project tests/SmokeTest

# build exe
dotnet publish src -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o dist
```
