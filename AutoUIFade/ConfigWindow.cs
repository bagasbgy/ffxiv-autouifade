using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AutoUIFade;

internal sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base("Auto UI Fade##AutoUIFadeConfig")
    {
        this.plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(430, 360),
            MaximumSize = new System.Numerics.Vector2(900, 900),
        };

        ShowCloseButton = true;
        RespectCloseHotkey = true;
    }

    public new void Toggle() => IsOpen = !IsOpen;

    public override void Draw()
    {
        if (!IsOpen)
            return;

        var cfg = plugin.Configuration;

        var enabled = cfg.Enabled;
        if (ImGui.Checkbox("Enable", ref enabled))
        {
            cfg.Enabled = enabled;
            cfg.Save();
        }

        ImGui.Separator();

        var showInCombat = cfg.ShowInCombat;
        if (ImGui.Checkbox("Show in combat", ref showInCombat))
        {
            cfg.ShowInCombat = showInCombat;
            cfg.Save();
        }

        var showWhenWeaponDrawn = cfg.ShowWhenWeaponDrawn;
        if (ImGui.Checkbox("Show when weapon is drawn", ref showWhenWeaponDrawn))
        {
            cfg.ShowWhenWeaponDrawn = showWhenWeaponDrawn;
            cfg.Save();
        }

        var showOnHover = cfg.ShowOnHover;
        if (ImGui.Checkbox("Show when mouse is over configured UI", ref showOnHover))
        {
            cfg.ShowOnHover = showOnHover;
            cfg.Save();
        }

        var showWhileCrafting = cfg.ShowWhileCrafting;
        if (ImGui.Checkbox("Show while crafting", ref showWhileCrafting))
        {
            cfg.ShowWhileCrafting = showWhileCrafting;
            cfg.Save();
        }

        var showWhileGathering = cfg.ShowWhileGathering;
        if (ImGui.Checkbox("Show while gathering", ref showWhileGathering))
        {
            cfg.ShowWhileGathering = showWhileGathering;
            cfg.Save();
        }

        var showInPvp = cfg.ShowInPvp;
        if (ImGui.Checkbox("Show in PvP", ref showInPvp))
        {
            cfg.ShowInPvp = showInPvp;
            cfg.Save();
        }

        var showInDutyOrInstance = cfg.ShowInDutyOrInstance;
        if (ImGui.Checkbox("Show in duties and instances", ref showInDutyOrInstance))
        {
            cfg.ShowInDutyOrInstance = showInDutyOrInstance;
            cfg.Save();
        }

        var showInNpcConversation = cfg.ShowInNpcConversation;
        if (ImGui.Checkbox("Show in NPC conversations", ref showInNpcConversation))
        {
            cfg.ShowInNpcConversation = showInNpcConversation;
            cfg.Save();
        }

        var showWhenTargetSelected = cfg.ShowWhenTargetSelected;
        if (ImGui.Checkbox("Show when a target is selected", ref showWhenTargetSelected))
        {
            cfg.ShowWhenTargetSelected = showWhenTargetSelected;
            cfg.Save();
        }

        var inactiveOpacity = cfg.InactiveOpacityPercent;
        if (ImGui.SliderFloat("Inactive opacity", ref inactiveOpacity, 0f, 100f, "%.0f%%"))
        {
            cfg.InactiveOpacityPercent = inactiveOpacity;
            cfg.Save();
        }

        var fadeDuration = cfg.FadeDurationSeconds;
        if (ImGui.SliderFloat("Fade duration", ref fadeDuration, 0f, 3f, "%.2f s"))
        {
            cfg.FadeDurationSeconds = fadeDuration;
            cfg.Save();
        }

        var delay = cfg.HideDelaySeconds;
        if (ImGui.SliderFloat("Hide delay", ref delay, 0f, 3f, "%.2f s"))
        {
            cfg.HideDelaySeconds = delay;
            cfg.Save();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Auto show/hide UI elements");
        ImGui.TextWrapped(
            "Choose which built-in UI segments fade while idle. Player Buffs and Debuffs uses the native _Status HUD addon.");

        void SaveAndReconfigure()
        {
            cfg.Normalize();
            cfg.Save();
            plugin.UIManager.Reconfigure();
        }

        void DrawSegment(string label, bool? selection, Action<bool> setSelection)
        {
            var selected = selection == true;
            if (ImGui.Checkbox(label, ref selected))
            {
                setSelection(selected);
                SaveAndReconfigure();
            }
        }

        DrawSegment("Hotbars", cfg.FadeHotbars, value => cfg.FadeHotbars = value);
        DrawSegment("Job gauges", cfg.FadeJobGauges, value => cfg.FadeJobGauges = value);
        DrawSegment("Parameter bar", cfg.FadeParameterBar, value => cfg.FadeParameterBar = value);
        DrawSegment("Experience bar", cfg.FadeExperienceBar, value => cfg.FadeExperienceBar = value);
        DrawSegment("Player buffs and debuffs", cfg.FadePlayerStatus, value => cfg.FadePlayerStatus = value);

        ImGui.Separator();
        ImGui.TextUnformatted("Custom List");
        ImGui.TextWrapped("Add native addon names here when a UI element is not covered by the built-in segments.");

        for (var i = 0; i < cfg.CustomAddonNames.Count; i++)
        {
            var value = cfg.CustomAddonNames[i];

            ImGui.PushID($"custom-{i}");

            var changed = ImGui.InputText("##name", ref value, 64);
            ImGui.SameLine();

            if (ImGui.Button("Remove"))
            {
                cfg.CustomAddonNames.RemoveAt(i);
                SaveAndReconfigure();
                ImGui.PopID();
                break;
            }

            if (changed)
            {
                cfg.CustomAddonNames[i] = value;
                SaveAndReconfigure();
            }

            ImGui.PopID();
        }

        if (ImGui.Button("Add custom addon"))
        {
            cfg.CustomAddonNames.Add("CustomAddon");
            SaveAndReconfigure();
        }

        ImGui.SameLine();

        if (ImGui.Button("Restore configured UI now"))
        {
            plugin.UIManager.RestoreAll();
        }
    }

    public void Dispose()
    {
    }
}
