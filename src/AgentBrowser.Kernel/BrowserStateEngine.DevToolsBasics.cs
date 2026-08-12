using System.Collections.Concurrent;
using System.Text.Json;
using AgentBrowser.Cdp;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    private readonly ConcurrentDictionary<string, BrowserDialogInfo> _dialogsByTarget = new(StringComparer.Ordinal);

    private void HandleDevToolsBasicEvent(JsonElement message)
    {
        if (!message.TryGetProperty("method", out var methodValue) || methodValue.ValueKind != JsonValueKind.String)
            return;
        var method = methodValue.GetString() ?? "";
        if (!message.TryGetProperty("sessionId", out var sessionValue) || sessionValue.ValueKind != JsonValueKind.String ||
            !_sessions.TryGetValue(sessionValue.GetString() ?? "", out var state))
            return;
        if (!message.TryGetProperty("params", out var p) || p.ValueKind != JsonValueKind.Object)
            return;

        if (method == "Page.javascriptDialogOpening")
        {
            _dialogsByTarget[state.TargetId] = new BrowserDialogInfo(
                state.LogicalId,
                GetString(p, "type"),
                GetString(p, "message"),
                NullIfEmpty(GetString(p, "defaultPrompt")),
                NullIfEmpty(GetString(p, "url")),
                true,
                DateTimeOffset.UtcNow);
        }
        else if (method == "Page.javascriptDialogClosed")
        {
            _dialogsByTarget.TryRemove(state.TargetId, out _);
        }
    }

    public async Task<BrowserDialogInfo?> DialogCurrentAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        await EnsureTargetStateAsync(target, cancellationToken);
        return _dialogsByTarget.TryGetValue(target.TargetId, out var info) ? info : null;
    }

    public async Task<object> DialogHandleAsync(
        string targetReference,
        bool accept,
        string? promptText = null,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        if (!_dialogsByTarget.TryGetValue(target.TargetId, out var dialog) || !dialog.Open)
            throw new InvalidOperationException($"Target {target.Id} has no current JavaScript dialog.");

        var parameters = new Dictionary<string, object?> { ["accept"] = accept };
        if (promptText is not null) parameters["promptText"] = promptText;
        await _cdp.SendAsync("Page.handleJavaScriptDialog", parameters, state.SessionId, cancellationToken);
        _dialogsByTarget.TryRemove(target.TargetId, out _);
        return new { target = target.Id, accepted = accept, promptTextProvided = promptText is not null };
    }

    public async Task<object> EmulateViewportAsync(
        string targetReference,
        int width,
        int height,
        double deviceScaleFactor = 1,
        bool mobile = false,
        bool touch = false,
        CancellationToken cancellationToken = default)
    {
        if (width is < 1 or > 16384 || height is < 1 or > 16384)
            throw new ArgumentOutOfRangeException(nameof(width), "Viewport dimensions must be between 1 and 16384 pixels.");
        if (deviceScaleFactor is <= 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(deviceScaleFactor));

        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        await _cdp.SendAsync("Emulation.setDeviceMetricsOverride", new
        {
            width,
            height,
            deviceScaleFactor,
            mobile,
            screenWidth = width,
            screenHeight = height,
            dontSetVisibleSize = false
        }, state.SessionId, cancellationToken);
        try { await _cdp.SendAsync("Emulation.setTouchEmulationEnabled", new { enabled = touch, maxTouchPoints = touch ? 1 : 0 }, state.SessionId, cancellationToken); }
        catch (CdpException) { if (touch) throw; }
        return new { target = target.Id, width, height, deviceScaleFactor, mobile, touch };
    }

    public async Task<object> EmulateCpuAsync(string targetReference, double rate, CancellationToken cancellationToken = default)
    {
        if (rate < 1 || rate > 100) throw new ArgumentOutOfRangeException(nameof(rate));
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        await _cdp.SendAsync("Emulation.setCPUThrottlingRate", new { rate }, state.SessionId, cancellationToken);
        return new { target = target.Id, rate };
    }

    public async Task<object> EmulateGeolocationAsync(
        string targetReference,
        double latitude,
        double longitude,
        double accuracy = 1,
        CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        await _cdp.SendAsync("Emulation.setGeolocationOverride", new { latitude, longitude, accuracy }, state.SessionId, cancellationToken);
        return new { target = target.Id, latitude, longitude, accuracy };
    }

    public async Task<object> EmulateLocaleAsync(string targetReference, string locale, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        await _cdp.SendAsync("Emulation.setLocaleOverride", new { locale }, state.SessionId, cancellationToken);
        return new { target = target.Id, locale };
    }

    public async Task<object> EmulateTimezoneAsync(string targetReference, string timezoneId, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        await _cdp.SendAsync("Emulation.setTimezoneOverride", new { timezoneId }, state.SessionId, cancellationToken);
        return new { target = target.Id, timezoneId };
    }

    public async Task<object> EmulateMediaAsync(
        string targetReference,
        string? media,
        IReadOnlyDictionary<string, string>? features,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var featureArray = (features ?? new Dictionary<string, string>())
            .Select(x => new { name = x.Key, value = x.Value }).ToArray();
        await _cdp.SendAsync("Emulation.setEmulatedMedia", new { media = media ?? "", features = featureArray }, state.SessionId, cancellationToken);
        return new { target = target.Id, media = media ?? "", features = featureArray };
    }

    public async Task<object> EmulateNetworkAsync(
        string targetReference,
        bool offline,
        double latencyMs,
        double downloadBytesPerSecond,
        double uploadBytesPerSecond,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        await _cdp.SendAsync("Network.emulateNetworkConditions", new
        {
            offline,
            latency = Math.Max(0, latencyMs),
            downloadThroughput = downloadBytesPerSecond,
            uploadThroughput = uploadBytesPerSecond
        }, state.SessionId, cancellationToken);
        return new { target = target.Id, offline, latencyMs, downloadBytesPerSecond, uploadBytesPerSecond };
    }

    public async Task<object> EmulateResetAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var reset = new Dictionary<string, bool>(StringComparer.Ordinal);

        reset["viewport"] = await TryResetAsync("Emulation.clearDeviceMetricsOverride", null, state, cancellationToken);
        reset["touch"] = await TryResetAsync("Emulation.setTouchEmulationEnabled", new { enabled = false, maxTouchPoints = 0 }, state, cancellationToken);
        reset["cpu"] = await TryResetAsync("Emulation.setCPUThrottlingRate", new { rate = 1 }, state, cancellationToken);
        reset["geolocation"] = await TryResetAsync("Emulation.clearGeolocationOverride", null, state, cancellationToken);
        reset["locale"] = await TryResetAsync("Emulation.setLocaleOverride", new { locale = "" }, state, cancellationToken);
        reset["timezone"] = await TryResetAsync("Emulation.setTimezoneOverride", new { timezoneId = "" }, state, cancellationToken);
        reset["media"] = await TryResetAsync("Emulation.setEmulatedMedia", new { media = "", features = Array.Empty<object>() }, state, cancellationToken);
        reset["network"] = await TryResetAsync("Network.emulateNetworkConditions", new { offline = false, latency = 0, downloadThroughput = -1, uploadThroughput = -1 }, state, cancellationToken);
        reset["headers"] = await TryResetAsync("Network.setExtraHTTPHeaders", new { headers = new { } }, state, cancellationToken);
        return new { target = target.Id, reset };
    }

    private async Task<bool> TryResetAsync(string method, object? parameters, TargetState state, CancellationToken cancellationToken)
    {
        try
        {
            await _cdp.SendAsync(method, parameters, state.SessionId, cancellationToken);
            return true;
        }
        catch (CdpException)
        {
            return false;
        }
    }
}
