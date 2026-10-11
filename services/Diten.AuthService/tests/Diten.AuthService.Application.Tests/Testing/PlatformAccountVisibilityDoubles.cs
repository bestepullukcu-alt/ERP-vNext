using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Application.Tests.Testing;

/// <summary>BL-521 FIX7 item 4 - the visibility is a REQUIRED argument of the repository and the list reader; a test that builds one
/// by hand says which caller it plays. This one is a caller that is not hidden from anything (a platform-door actor, an internal call).</summary>
internal sealed class ShowEveryAccount : IPlatformAccountVisibility
{
    public bool HidesPlatformAccounts => false;
}

/// <summary>The other caller: a tenant actor, from whom platform accounts are absent.</summary>
internal sealed class HideAccountsFromCaller : IPlatformAccountVisibility
{
    public bool HidesPlatformAccounts => true;
}
