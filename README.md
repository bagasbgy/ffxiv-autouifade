# Auto UI Fade

FFXIV Dalamud plugin that fades selected hotbars and HUD elements while they are idle, then restores them when they are needed, including during NPC conversations.

## UI selection

The configuration window uses a checklist of built-in UI segments:

- Hotbars
- Job gauges
- Parameter bar
- Experience bar
- Player buffs and debuffs

Player buffs and debuffs use FFXIV's native `_Status` and `_StatusCustom0` through `_StatusCustom3` HUD addons. The custom status addons support separate buff, debuff, and other-status HUD layouts. Other native addon names can be added through the **Custom List** section when a UI element is not covered by a built-in segment.

Existing configurations keep their selected addon names when upgraded. Partial or custom legacy selections are moved into Custom List automatically.

## Install through Dalamud

1. Open Dalamud settings and go to **Experimental**.
2. Add this custom plugin repository:

    `https://bagasbgy.github.io/ffxiv-autouifade/repo.json`

3. Save the settings, open the Dalamud plugin installer, and install **Auto UI Fade**.

The repository catalog is published through GitHub Pages and points to the latest tagged GitHub Release package.

Use `/autouifade` in game to open the configuration window.

## Build locally

From the repository root:

```powershell
dotnet build AutoUIFade/AutoUIFade.csproj -c Release
```

The Dalamud package is generated at `AutoUIFade/bin/Release/AutoUIFade/latest.zip`.

## Publish a release

Create and push a semantic version tag, for example:

```powershell
git tag v0.1.1
git push origin v0.1.1
```

GitHub Actions builds the plugin, creates the GitHub Release asset, and updates the custom repository catalog.

## Links

- [GitHub repository](https://github.com/bagasbgy/ffxiv-autouifade)
- [MIT license](LICENSE)
