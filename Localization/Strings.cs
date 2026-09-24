using System.Globalization;
using System.Resources;

namespace MiddleClickToClose;

/// <summary>
/// ユーザーに表示する文言。Windows の表示言語 (CurrentUICulture) に応じて Strings.*.resx から引く。
/// 対応していない言語では英語 (Strings.resx) を使う。
/// </summary>
internal static class Strings
{
    private static readonly ResourceManager s_resourceManager =
        new(typeof(Strings).FullName!, typeof(Strings).Assembly);

    internal static string TrayTooltip => Get(nameof(TrayTooltip));

    internal static string ExitMenu => Get(nameof(ExitMenu));

    internal static string EmbeddedResourceNotFound(string name) =>
        string.Format(CultureInfo.CurrentCulture, Get(nameof(EmbeddedResourceNotFound)), name);

    private static string Get(string key) =>
        s_resourceManager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new MissingManifestResourceException($"String resource '{key}' is missing.");
}
