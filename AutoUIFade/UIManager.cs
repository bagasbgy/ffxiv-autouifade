using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoUIFade;

internal unsafe sealed class UIManager
{
    private sealed class TrackedAddon
    {
        public string Name { get; init; } = string.Empty;
        public nint Address { get; set; }
        public nint VisualRootAddress { get; set; }
        public byte OriginalRootAlpha { get; set; }
        public byte? AppliedRootAlpha { get; set; }
        public bool Captured { get; set; }
        public Dictionary<nint, byte> OriginalNodeAlphas { get; } = new();
    }

    private readonly Configuration configuration;
    private readonly Dictionary<string, TrackedAddon> tracked = new(StringComparer.Ordinal);
    private readonly HashSet<string> loggedAddonNames = new(StringComparer.Ordinal);

    private bool currentlyShown = true;
    private DateTime hideRequestedAt = DateTime.MaxValue;
    private bool loggedNoAddons;
    private float currentOpacity = 1f;
    private float targetOpacity = 1f;
    private float transitionStartOpacity = 1f;
    private DateTime transitionStartedAt = DateTime.UtcNow;

    internal UIManager(Configuration configuration)
    {
        this.configuration = configuration;
    }

    internal void Update(bool shouldShow)
    {
        RefreshTrackedAddons();

        if (shouldShow)
        {
            hideRequestedAt = DateTime.MaxValue;
            currentlyShown = true;
        }
        else if (currentlyShown)
        {
            if (hideRequestedAt == DateTime.MaxValue)
                hideRequestedAt = DateTime.UtcNow;

            var elapsed = DateTime.UtcNow - hideRequestedAt;
            if (elapsed.TotalSeconds >= configuration.HideDelaySeconds)
            {
                currentlyShown = false;
            }
        }

        var desiredOpacity = currentlyShown
            ? 1f
            : configuration.InactiveOpacityPercent / 100f;

        AnimateTo(desiredOpacity);
    }

    internal bool IsMouseOverConfiguredUI()
    {
        var mouse = ImGui.GetMousePos();

        foreach (var addonName in ManagedAddonNames())
        {
            var addon = Plugin.GameGui.GetAddonByName(addonName);
            if (addon.IsNull || !addon.IsReady)
                continue;

            var x = addon.X;
            var y = addon.Y;
            var w = addon.ScaledWidth;
            var h = addon.ScaledHeight;

            if (w <= 0 || h <= 0)
                continue;

            if (mouse.X >= x && mouse.X <= x + w &&
                mouse.Y >= y && mouse.Y <= y + h)
                return true;
        }

        return false;
    }

    internal void RestoreAll()
    {
        hideRequestedAt = DateTime.MaxValue;
        currentlyShown = true;
        currentOpacity = 1f;
        targetOpacity = 1f;
        transitionStartOpacity = 1f;
        transitionStartedAt = DateTime.UtcNow;

        foreach (var addonState in tracked.Values)
        {
            if (!addonState.Captured || addonState.Address == nint.Zero)
                continue;

            try
            {
                var addon = Plugin.GameGui.GetAddonByName(addonState.Name);
                if (addon.IsNull || !addon.IsReady || addon.Address == nint.Zero ||
                    addon.Address != addonState.Address)
                    continue;

                var rootNode = GetVisualRootNode(addonState.Name, addon.Address);
                if (rootNode == null)
                    continue;

                if (IsPlayerStatus(addonState.Name))
                    RestoreNodeAlphas(rootNode, addonState);
                else
                    rootNode->SetAlpha(addonState.OriginalRootAlpha);

                addonState.AppliedRootAlpha = null;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, $"Failed to restore UI addon {addonState.Name}.");
            }
        }
    }

    internal void Reconfigure()
    {
        RestoreAll();
        tracked.Clear();
        loggedAddonNames.Clear();
        loggedNoAddons = false;
    }

    private void RefreshTrackedAddons()
    {
        var foundCount = 0;

        foreach (var name in ManagedAddonNames())
        {
            var addon = Plugin.GameGui.GetAddonByName(name);
            if (addon.IsNull || !addon.IsReady)
            {
                if (loggedAddonNames.Add($"missing:{name}"))
                    Plugin.Log.Warning($"Configured {GetAddonCategory(name)} addon was not found: {name}.");
                continue;
            }

            foundCount++;
            if (loggedAddonNames.Add(name))
                Plugin.Log.Information($"Found configured {GetAddonCategory(name)} addon: {name}.");

            if (!tracked.TryGetValue(name, out var state))
            {
                state = new TrackedAddon { Name = name };
                tracked[name] = state;
            }

            if (state.Address != addon.Address)
            {
                state.Address = addon.Address;
                state.VisualRootAddress = nint.Zero;
                state.Captured = false;
                state.AppliedRootAlpha = null;
                state.OriginalNodeAlphas.Clear();
            }

            var rootNode = GetVisualRootNode(name, addon.Address);
            if (rootNode == null)
                continue;

            var rootAddress = (nint)rootNode;
            if (!state.Captured || state.VisualRootAddress != rootAddress)
            {
                state.VisualRootAddress = rootAddress;
                state.OriginalRootAlpha = IsJobGauge(name)
                    ? (byte)255
                    : rootNode->Alpha_2;
                state.AppliedRootAlpha = null;
                state.Captured = true;
                state.OriginalNodeAlphas.Clear();
            }

            if (IsPlayerStatus(name))
                CaptureOriginalNodeAlphas(rootNode, state);
        }

        if (foundCount > 0)
        {
            loggedNoAddons = false;
        }
        else if (!loggedNoAddons)
        {
            loggedNoAddons = true;
            Plugin.Log.Warning("No configured UI addons were found. Standard names include _ActionBar, JobHudPLD0, and _Status.");
        }
    }

    private void AnimateTo(float desiredOpacity)
    {
        if (Math.Abs(desiredOpacity - targetOpacity) > 0.001f)
        {
            transitionStartOpacity = currentOpacity;
            targetOpacity = desiredOpacity;
            transitionStartedAt = DateTime.UtcNow;
        }

        var duration = configuration.FadeDurationSeconds;
        var progress = duration <= 0f
            ? 1f
            : Math.Clamp((float)(DateTime.UtcNow - transitionStartedAt).TotalSeconds / duration, 0f, 1f);
        currentOpacity = transitionStartOpacity + ((targetOpacity - transitionStartOpacity) * progress);
        SetOpacity(currentOpacity);
    }

    private void SetOpacity(float opacity)
    {
        foreach (var name in ManagedAddonNames())
        {
            var addon = Plugin.GameGui.GetAddonByName(name);
            if (addon.IsNull || !addon.IsReady)
                continue;

            var native = (AtkUnitBase*)addon.Address;
            var rootNode = GetVisualRootNode(name, addon.Address);
            if (native == null || rootNode == null)
                continue;

            if (!tracked.TryGetValue(name, out var state))
                continue;

            var alpha = (byte)Math.Clamp(
                Math.Round(state.OriginalRootAlpha * opacity),
                0d,
                255d);

            if (IsPlayerStatus(name))
            {
                SetPlayerStatusOpacity(rootNode, state, opacity);
                state.AppliedRootAlpha = alpha;
            }
            else if (state.AppliedRootAlpha != alpha || rootNode->Alpha_2 != alpha)
            {
                rootNode->SetAlpha(alpha);
                state.AppliedRootAlpha = alpha;
            }
        }
    }

    private void CaptureOriginalNodeAlphas(AtkResNode* node, TrackedAddon state)
    {
        if (node == null)
            return;

        state.OriginalNodeAlphas.TryAdd((nint)node, node->Alpha_2);

        for (var child = node->ChildNode; child != null; child = child->NextSiblingNode)
            CaptureOriginalNodeAlphas(child, state);
    }

    private void SetPlayerStatusOpacity(AtkResNode* node, TrackedAddon state, float opacity)
    {
        if (node == null)
            return;

        var originalAlpha = state.OriginalNodeAlphas.TryGetValue((nint)node, out var capturedAlpha)
            ? capturedAlpha
            : node->Alpha_2;
        var alpha = (byte)Math.Clamp(Math.Round(originalAlpha * opacity), 0d, 255d);
        if (node->Alpha_2 != alpha)
            node->SetAlpha(alpha);

        for (var child = node->ChildNode; child != null; child = child->NextSiblingNode)
            SetPlayerStatusOpacity(child, state, opacity);
    }

    private void RestoreNodeAlphas(AtkResNode* node, TrackedAddon state)
    {
        if (node == null)
            return;

        if (state.OriginalNodeAlphas.TryGetValue((nint)node, out var originalAlpha))
            node->SetAlpha(originalAlpha);

        for (var child = node->ChildNode; child != null; child = child->NextSiblingNode)
            RestoreNodeAlphas(child, state);
    }

    private AtkResNode* GetVisualRootNode(string addonName, nint address)
    {
        if (address == nint.Zero)
            return null;

        if (IsJobGauge(addonName))
        {
            var jobHud = (AddonJobHud*)address;
            if (jobHud != null && jobHud->JobHudRootNode != null)
                return jobHud->JobHudRootNode;
        }

        var addon = (AtkUnitBase*)address;
        return addon == null ? null : addon->RootNode;
    }

    private bool IsJobGauge(string addonName)
    {
        return Configuration.CreateDefaultJobGaugeAddonNames().Contains(addonName, StringComparer.Ordinal);
    }

    private bool IsPlayerStatus(string addonName)
    {
        return Configuration.CreateDefaultPlayerStatusAddonNames().Contains(addonName, StringComparer.Ordinal);
    }

    private IEnumerable<string> ManagedAddonNames()
    {
        return configuration.GetConfiguredAddonNames()
            .Distinct(StringComparer.Ordinal);
    }

    private string GetAddonCategory(string addonName)
    {
        if (Configuration.CreateDefaultJobGaugeAddonNames().Contains(addonName, StringComparer.Ordinal))
            return "job gauge";

        if (Configuration.CreateDefaultParameterBarAddonNames().Contains(addonName, StringComparer.Ordinal))
            return "parameter bar";

        if (Configuration.CreateDefaultExperienceBarAddonNames().Contains(addonName, StringComparer.Ordinal))
            return "experience bar";

        if (Configuration.CreateDefaultPlayerStatusAddonNames().Contains(addonName, StringComparer.Ordinal))
            return "player status";

        if (Configuration.CreateDefaultHotbarAddonNames().Contains(addonName, StringComparer.Ordinal))
            return "hotbar";

        return "custom UI";
    }
}
