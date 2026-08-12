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

if (failures.Count > 0)
{
    Console.Error.WriteLine("Build 002 smoke FAILED");
    foreach (var failure in failures) Console.Error.WriteLine("- " + failure);
    return 1;
}

Console.WriteLine("Build 002 smoke PASS");
Console.WriteLine("identity cases: 6");
Console.WriteLine("false hard rebinds in deterministic hostile set: 0");
return 0;