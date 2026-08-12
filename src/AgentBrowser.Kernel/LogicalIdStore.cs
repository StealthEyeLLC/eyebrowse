using System.Text.Json;

namespace AgentBrowser.Kernel;

internal sealed class LogicalIdStore
{
    private readonly object _gate = new();
    private readonly string _pathName;
    private State _state;

    public LogicalIdStore(string? pathName = null)
    {
        _pathName = pathName ?? Path.Combine(BrowserRuntime.RuntimeDir, $"logical-ids-{BrowserRuntime.ProfileName}.json");
        _state = Load();
    }

    public string TargetIdFor(string browserTargetId)
    {
        lock (_gate)
        {
            if (_state.Targets.TryGetValue(browserTargetId, out var existing))
                return existing;
            var id = $"t_{++_state.NextTarget}";
            _state.Targets[browserTargetId] = id;
            Save();
            return id;
        }
    }

    public string NewDocumentId()
    {
        lock (_gate)
        {
            var id = $"d_{++_state.NextDocument}";
            Save();
            return id;
        }
    }

    public string NewElementId()
    {
        lock (_gate)
        {
            var id = $"e_{++_state.NextElement}";
            Save();
            return id;
        }
    }

    public void ObserveExisting(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var underscore = id.LastIndexOf('_');
        if (underscore < 0 || !long.TryParse(id.AsSpan(underscore + 1), out var number)) return;

        lock (_gate)
        {
            if (id.StartsWith("t_", StringComparison.Ordinal) && number > _state.NextTarget)
                _state.NextTarget = number;
            else if (id.StartsWith("d_", StringComparison.Ordinal) && number > _state.NextDocument)
                _state.NextDocument = number;
            else if (id.StartsWith("e_", StringComparison.Ordinal) && number > _state.NextElement)
                _state.NextElement = number;
            else
                return;
            Save();
        }
    }

    private State Load()
    {
        try
        {
            if (!File.Exists(_pathName)) return new State();
            return JsonSerializer.Deserialize<State>(File.ReadAllText(_pathName)) ?? new State();
        }
        catch
        {
            return new State();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_pathName)!);
        var temp = _pathName + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _pathName, true);
    }

    private sealed class State
    {
        public long NextTarget { get; set; }
        public long NextDocument { get; set; }
        public long NextElement { get; set; }
        public Dictionary<string, string> Targets { get; set; } = new(StringComparer.Ordinal);
    }
}