# Cyber Mod Manager

A small, free mod manager for Cyberpunk 2077.

## Using it

1. Run `CyberModManager.exe`.
2. The game folder is found automatically for Steam, GOG and Epic. If it isn't, click **Browse...** and pick the folder that contains `bin`, `archive`, `r6`.
3. Drag `.zip` / `.7z` / `.rar` mod files onto the window, or click **Add mods...**.
4. Select mods, then click **Install**. Select mods, then click **Uninstall** to remove them.

Other actions: **F2** or double-click renames a mod, **Delete** removes it from the list, and right-click opens a menu. Dropping a file with the same name as an existing mod replaces it, which is how you update a mod.

## What it does for you

- **Copies each archive into its own data folder** (`ModManagerData\` next to the exe), so you can delete your downloads.
- **Records every file it installs**, so uninstalling removes only those files, plus any folders it created that are now empty.
- **Handles overlapping mods:** if a mod overwrites a file from the game or from another mod, the old file is backed up and put back when you uninstall. This works in any uninstall order.
- **Fixes common packaging mistakes:** removes an extra wrapper folder (`MyMod v1.2\archive\...`), puts loose `.archive` files in `archive\pc\mod`, and leaves readmes and screenshots out of the game folder.
- **Password protected archives:** it asks for the password once and remembers it.
- **If an install fails partway** (for example because the game is running and a file is locked), it's rolled back automatically.

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
