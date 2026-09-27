using System.Runtime.CompilerServices;
using MyBudget.Domain;

namespace MyBudget.Api.Tests.Setup;

/// <summary>
/// The app pins formatting to en-US at startup; the tests assert on those strings, so they have to
/// run under the same culture. Without this they pass on a developer machine with LANG set and fail
/// on a CI runner without one, where "$210.00" comes out as "¤210.00".
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Init() => AppCulture.Apply();
}
