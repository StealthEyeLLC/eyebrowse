using System.Text.Json;
using AgentBrowser.Cdp;
using AgentBrowser.State;

var failures = new List<string>();

void Expect(string name, bool condition, string detail)
{
    if (!condition) failures.Add($"{name}: {detail}");
}

static RebindingCandidate C(
    string id,
    int incarnation,
    int backend,
    string role,
    string name,
    params (string Key, string Value)[] attrs) =>
    new(id, incarnation, backend, role, name, null, null,
        attrs.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));

var uniqueStrong = ConservativeRebinding.Resolve(
    C("", 1, 20, "button", "Save", ("data-testid", "save")),
    new[] { C("e_4", 1, 10, "button", "Save", ("data-testid", "save")) },
    new HashSet<int> { 20 });
Expect("unique-strong-rebind", uniqueStrong.Outcome == IdentityOutcomes.Rebound && uniqueStrong.Id == "e_4" && uniqueStrong.Incarnation == 2,
    $"expected rebound e_4@2, got {uniqueStrong.Outcome} {uniqueStrong.Id}@{uniqueStrong.Incarnation}");

var weakOnly = ConservativeRebinding.Resolve(
    C("", 1, 20, "button", "Save"),
    new[] { C("e_4", 1, 10, "button", "Save") },
    new HashSet<int> { 20 });
Expect("weak-role-name-abstains", weakOnly.Outcome == IdentityOutcomes.Stale && weakOnly.Id is null,
    $"weak role/name must not hard-rebind, got {weakOnly.Outcome} {weakOnly.Id}");

var ambiguous = ConservativeRebinding.Resolve(
    C("", 1, 30, "link", "Details", ("href", "/item/42")),
    new[]
    {
        C("e_8", 1, 10, "link", "Details", ("href", "/item/42")),
        C("e_9", 1, 11, "link", "Details", ("href", "/item/42"))
    },
    new HashSet<int> { 30 });
Expect("ambiguous-abstains", ambiguous.Outcome == IdentityOutcomes.Ambiguous && ambiguous.Id is null && ambiguous.Candidates.Count == 2,
    $"expected ambiguous two candidates, got {ambiguous.Outcome} {ambiguous.Id} candidates={ambiguous.Candidates.Count}");

var roleMismatch = ConservativeRebinding.Resolve(
    C("", 1, 40, "textbox", "Search", ("id", "q")),
    new[] { C("e_12", 3, 14, "button", "Search", ("id", "q")) },
    new HashSet<int> { 40 });
Expect("role-mismatch-stale", roleMismatch.Outcome == IdentityOutcomes.Stale,
    $"role mismatch must not rebind, got {roleMismatch.Outcome}");

var survivingBackendExcluded = ConservativeRebinding.Resolve(
    C("", 1, 50, "button", "Save", ("data-testid", "save")),
    new[] { C("e_21", 2, 45, "button", "Save", ("data-testid", "save")) },
    new HashSet<int> { 45, 50 });
Expect("surviving-old-node-not-successor", survivingBackendExcluded.Outcome == IdentityOutcomes.Stale,
    $"a still-live prior backend node must not be stolen as successor, got {survivingBackendExcluded.Outcome}");

var attributeUnique = ConservativeRebinding.Resolve(
    C("", 1, 60, "link", "Open", ("href", "/a")),
    new[]
    {
        C("e_31", 4, 51, "link", "Open", ("href", "/a")),
        C("e_32", 1, 52, "link", "Open", ("href", "/b"))
    },
    new HashSet<int> { 60 });
Expect("strong-attribute-disambiguates", attributeUnique.Outcome == IdentityOutcomes.Rebound && attributeUnique.Id == "e_31" && attributeUnique.Incarnation == 5,
    $"href should uniquely preserve e_31 concept, got {attributeUnique.Outcome} {attributeUnique.Id}@{attributeUnique.Incarnation}");

var priorSplit = C("e_40", 2, 61, "button", "Save", ("data-testid", "save"));
var equalSuccessors = new[]
{
    C("", 1, 70, "button", "Save", ("data-testid", "save")),
    C("", 1, 71, "button", "Save", ("data-testid", "save"))
};
var splitA = ConservativeRebinding.ResolveAgainstSurface(equalSuccessors[0], new[] { priorSplit }, equalSuccessors, new HashSet<int> { 70, 71 });
var splitB = ConservativeRebinding.ResolveAgainstSurface(equalSuccessors[1], new[] { priorSplit }, equalSuccessors, new HashSet<int> { 70, 71 });
Expect("one-prior-two-equal-successors-a-abstains", splitA.Outcome == IdentityOutcomes.Ambiguous && splitA.Id is null,
    $"equal successor A must be ambiguous, got {splitA.Outcome} {splitA.Id}");
Expect("one-prior-two-equal-successors-b-abstains", splitB.Outcome == IdentityOutcomes.Ambiguous && splitB.Id is null,
    $"equal successor B must be ambiguous, got {splitB.Outcome} {splitB.Id}");

var priorWeighted = C("e_50", 3, 62, "link", "Open", ("data-testid", "open-a"), ("href", "/a"));
var weightedSuccessors = new[]
{
    C("", 1, 80, "link", "Open", ("data-testid", "open-a"), ("href", "/a")),
    C("", 1, 81, "link", "Open", ("href", "/a"))
};
var weightedStrong = ConservativeRebinding.ResolveAgainstSurface(weightedSuccessors[0], new[] { priorWeighted }, weightedSuccessors, new HashSet<int> { 80, 81 });
var weightedWeak = ConservativeRebinding.ResolveAgainstSurface(weightedSuccessors[1], new[] { priorWeighted }, weightedSuccessors, new HashSet<int> { 80, 81 });
Expect("one-prior-clear-best-successor-rebinds", weightedStrong.Outcome == IdentityOutcomes.Rebound && weightedStrong.Id == "e_50" && weightedStrong.Incarnation == 4,
    $"strong successor should preserve e_50@4, got {weightedStrong.Outcome} {weightedStrong.Id}@{weightedStrong.Incarnation}");
Expect("one-prior-weaker-current-does-not-steal", weightedWeak.Outcome == IdentityOutcomes.Stale && weightedWeak.Id is null,
    $"weaker current object must not reuse e_50, got {weightedWeak.Outcome} {weightedWeak.Id}");
var protocolJson = """
{
  "version": {"major":"1","minor":"3"},
  "unknownTopLevel": {"future": true},
  "domains": [
    {
      "domain": "Stable",
      "commands": [
        {"name":"doThing"},
        {"name":"oldThing","deprecated":true,"futureField":{"x":1}}
      ],
      "events": [{"name":"changed"}]
    },
    {
      "domain": "Experimental",
      "experimental": true,
      "commands": [{"name":"tryThing","experimental":true}],
      "events": [{"name":"updated","experimental":true}]
    }
  ]
}
""";
using (var protocolDocument = JsonDocument.Parse(protocolJson))
{
    var summary = CdpDiscovery.ParseProtocolSummary(protocolDocument.RootElement);
    Expect("capability-domain-present", summary.HasDomain("Stable") && summary.HasDomain("Experimental"), "expected both domains present");
    Expect("capability-domain-absent", !summary.HasDomain("Missing"), "missing domain must remain absent");
    Expect("capability-command-present", summary.Supports("Stable.doThing") && summary.Supports("Experimental.tryThing"), "expected commands present");
    Expect("capability-command-absent", !summary.Supports("Stable.futureThing"), "unknown command must remain unsupported");
    Expect("capability-event-present", summary.Events.Contains("Stable.changed") && summary.Events.Contains("Experimental.updated"), "expected events present");
    Expect("capability-experimental-facet", summary.Describe("Experimental.tryThing") is { Experimental: true, Deprecated: false }, "experimental facet not preserved");
    Expect("capability-deprecated-facet", summary.Describe("Stable.oldThing") is { Experimental: false, Deprecated: true }, "deprecated facet not preserved");
    Expect("capability-unknown-schema-tolerated", summary.DomainCount == 2 && summary.Facets.Count >= 7, "future/unknown fields should not break parsing");
}
if (failures.Count > 0)
{
    Console.Error.WriteLine("Build 002 smoke FAILED");
    foreach (var failure in failures) Console.Error.WriteLine("- " + failure);
    return 1;
}

Console.WriteLine("Build 002 smoke PASS");
Console.WriteLine("identity cases: 10");
Console.WriteLine("false hard rebinds in deterministic hostile set: 0");
return 0;
