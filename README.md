# RIFTIX

<img src="Assets/logo.jpg" alt="RIFTIX logo" width="256" />

RIFTIX is a mod menu template for Gorilla Tag with custom categories and maximum customization. Based on ii's Stupid Template.

## Installation

1. Install [BepInEx 5 (x64)](https://github.com/BepInEx/BepInEx/releases) for Gorilla Tag
2. Download the latest release from the [Releases page](../../releases/latest)
3. Extract `RIFTIX.dll` into `Gorilla Tag/BepInEx/plugins/RIFTIX/`

## Building from source

1. Clone this repository
2. Open `Directory.Build.props` and set `<GamePath>` to your Gorilla Tag install directory (default: `C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag`)
3. Build:
   ```
   dotnet build
   ```
   The DLL is copied to your BepInEx plugins folder automatically.

### Visual Studio users: fixing the Publicize error (0x800711C7)

If Visual Studio fails with:

> The 'Publicize' task failed unexpectedly... An Application Control policy has blocked this file (0x800711C7)

this is caused by **Smart App Control** on Windows 11 blocking `Assembly-CSharp.dll`. Use one of these fixes:

- **Recommended:** build from a terminal with `dotnet build` instead of Visual Studio's Build button
- Or open **Windows Security > App & browser control > Smart App Control** and turn it off (note: this cannot be re-enabled without reinstalling Windows)
- Or add your Gorilla Tag folder and project folder as exclusions in **Windows Security > Virus & threat protection > Exclusions**

## Customizing

- Menu name and version: `Constants.cs`
- Menu visuals: `Menu/Settings.cs`
- Buttons: `Menu/Buttons.cs`

## Credits

- Original template by [@goldentrophy](https://github.com/goldentrophy) and [@zlothyy](https://github.com/zlothyy)
