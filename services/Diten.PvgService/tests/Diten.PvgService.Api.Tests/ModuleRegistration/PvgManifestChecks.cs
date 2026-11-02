using System.Text.RegularExpressions;

namespace Diten.PvgService.Api.Tests.ModuleRegistration;

// WP-0230-DL11: the pure checks behind AC-36 and AC-37. Each takes texts (or paths and texts) and returns findings, so a
// fixture held in the test code can prove the check itself is not vacuous.

// AC-36 (b), (d): the view routes and the page gates, read from the controller text.
internal static class PvgControllerText
{
    private static readonly Regex ClassRoute = new(
        @"\[Route\(""(?<route>[^""]*)""\)\]\s*public\s+sealed\s+class",
        RegexOptions.CultureInvariant);

    private static readonly Regex HttpGet = new(
        @"\[HttpGet\(""(?<template>[^""]*)""\)\]",
        RegexOptions.CultureInvariant);

    private static readonly Regex RequirePageCall = new(
        @"RequirePage\((?<constant>\w+Permission)\)",
        RegexOptions.CultureInvariant);

    private static readonly Regex PermissionConstant = new(
        @"private\s+const\s+string\s+(?<name>\w+Permission)\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.CultureInvariant);

    // Every [HttpGet] whose template does not start with "api/", as (route, RequirePage constant name or null).
    public static IReadOnlyList<(string Route, string? GateConstant)> ViewRoutes(string controllerText)
    {
        var classRoute = ClassRoute.Match(controllerText);
        if (!classRoute.Success)
        {
            throw new InvalidOperationException("The controller class [Route] was not found.");
        }

        var prefix = "/" + classRoute.Groups["route"].Value.Trim('/');
        var gets = HttpGet.Matches(controllerText);
        var routes = new List<(string Route, string? GateConstant)>();
        for (var i = 0; i < gets.Count; i++)
        {
            var template = gets[i].Groups["template"].Value;
            if (template.StartsWith("api/", StringComparison.Ordinal))
            {
                continue;
            }

            var end = i + 1 < gets.Count ? gets[i + 1].Index : controllerText.Length;
            var block = controllerText[gets[i].Index..end];
            var gate = RequirePageCall.Match(block);
            routes.Add((template.Length == 0 ? prefix : prefix + "/" + template, gate.Success ? gate.Groups["constant"].Value : null));
        }

        return routes;
    }

    // The *Permission string constants declared in the controller: name to value.
    public static IReadOnlyDictionary<string, string> PermissionConstants(string controllerText) =>
        PermissionConstant.Matches(controllerText)
            .ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value, StringComparer.Ordinal);
}

// AC-36 (e): the (page, button) pairs the UI shows, read from the four files.
internal static class PvgUiButtonExtractor
{
    public const string ListPage = "CASE_INTAKE_TRIAGE";
    public const string CreatePage = "CASE_INTAKE_TRIAGE_CREATE";
    public const string EditPage = "CASE_INTAKE_TRIAGE_EDIT";
    public const string DetailsPage = "CASE_INTAKE_TRIAGE_DETAILS";

    private static readonly Regex CreateLink = new(
        @"<a\s[^>]*href=""/Pharmacovigilance/CaseIntakeTriage/Create""",
        RegexOptions.CultureInvariant);

    private static readonly Regex EditLink = new(
        @"<a\s[^>]*href=""/Pharmacovigilance/CaseIntakeTriage/Edit/",
        RegexOptions.CultureInvariant);

    private static readonly Regex RowActionKey = new(@"\bkey:\s*'(?<key>\w+)'", RegexOptions.CultureInvariant);

    private static readonly Regex FormSubmit = new(
        @"<button\s(?=[^>]*type=""submit"")(?=[^>]*form=""pvg-case-intake-form"")[^>]*>",
        RegexOptions.CultureInvariant);

    private static readonly Regex FormTag = new(@"<form\b[^>]*>", RegexOptions.CultureInvariant);

    private static readonly Regex CommandFormId = new(@"\bid=""pvg-(?<code>[a-z]+)-form""", RegexOptions.CultureInvariant);

    public static IReadOnlyList<(string Page, string Action)> Extract(
        string indexCshtml,
        string indexJs,
        string formCshtml,
        string detailsCshtml)
    {
        var pairs = new List<(string Page, string Action)>();

        // The list: the link to Create.
        foreach (Match _ in CreateLink.Matches(indexCshtml))
        {
            pairs.Add((ListPage, "CREATE"));
        }

        // The list: each key of the renderActions call (quickView opens Details, edit opens Edit).
        var call = indexJs.IndexOf("renderActions(", StringComparison.Ordinal);
        while (call >= 0)
        {
            var end = indexJs.IndexOf("]);", call, StringComparison.Ordinal);
            var segment = end > call ? indexJs[call..end] : indexJs[call..];
            foreach (Match match in RowActionKey.Matches(segment))
            {
                pairs.Add((ListPage, ActionCodeOfKey(match.Groups["key"].Value)));
            }

            call = indexJs.IndexOf("renderActions(", call + 1, StringComparison.Ordinal);
        }

        // The form's submit: one form serves the Create page and the Edit page.
        if (FormSubmit.IsMatch(formCshtml))
        {
            pairs.Add((CreatePage, "SAVE"));
            pairs.Add((EditPage, "SAVE"));
        }

        // Details: the link to Edit and each command form.
        foreach (Match _ in EditLink.Matches(detailsCshtml))
        {
            pairs.Add((DetailsPage, "EDIT"));
        }

        foreach (Match tag in FormTag.Matches(detailsCshtml))
        {
            if (!tag.Value.Contains("data-pvg-command-form", StringComparison.Ordinal))
            {
                continue;
            }

            var id = CommandFormId.Match(tag.Value);
            if (id.Success)
            {
                pairs.Add((DetailsPage, id.Groups["code"].Value.ToUpperInvariant()));
            }
        }

        return pairs;
    }

    private static string ActionCodeOfKey(string key) => key switch
    {
        "quickView" => "VIEW",
        "edit" => "EDIT",
        _ => key.ToUpperInvariant()
    };
}

// AC-37 (c): every setting of the registration values in committed configuration: a JSON section or colon spelling
// ("PlatformRegistration") and the environment spelling ("PlatformRegistration__*"). One finding per file and spelling.
internal static class PvgRegistrationValueScan
{
    private static readonly (string Spelling, Regex Pattern)[] Spellings =
    [
        ("section", new Regex(@"(?<![A-Za-z0-9_])PlatformRegistration(?![A-Za-z0-9_])", RegexOptions.CultureInvariant)),
        ("underscore", new Regex(@"PlatformRegistration__", RegexOptions.CultureInvariant))
    ];

    public static IReadOnlyList<(string Path, string Spelling)> Find(IEnumerable<(string Path, string Text)> files)
    {
        var findings = new List<(string Path, string Spelling)>();
        foreach (var (path, text) in files)
        {
            foreach (var (spelling, pattern) in Spellings)
            {
                if (pattern.IsMatch(text))
                {
                    findings.Add((path, spelling));
                }
            }
        }

        return findings;
    }
}
