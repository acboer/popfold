using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Popfold;

/// <summary>Uma versão publicada no GitHub que é mais nova do que a instalada.</summary>
public sealed record UpdateInfo(Version Version, string Tag, string PageUrl);

/// <summary>
/// Pergunta ao GitHub qual é a última versão publicada do Popfold. Só lê a API pública de releases:
/// não manda nenhum dado do usuário e não baixa nada (o aviso abre a página da release no navegador).
/// </summary>
public static class UpdateChecker
{
    public const string Repo = "acboer/popfold";
    public const string ReleasesPage = "https://github.com/" + Repo + "/releases/latest";
    private const string LatestApi = "https://api.github.com/repos/" + Repo + "/releases/latest";

    private static readonly HttpClient Http = CreateHttp();

    public static Version Current
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    /// <summary>Texto curto da versão atual, ex.: "0.4.0".</summary>
    public static string CurrentText => Current.ToString(3);

    private static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Name}/{CurrentText}");
        h.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return h;
    }

    /// <summary>
    /// A versão nova, se houver; null se já está na última. Lança exceção se não conseguiu perguntar
    /// (sem internet, GitHub fora do ar, limite de consultas).
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var resp = await Http.GetAsync(LatestApi).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        var root = doc.RootElement;

        string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        string page = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? ReleasesPage : ReleasesPage;
        if (!TryParseTag(tag, out var latest)) return null;

        // Só abre páginas do próprio repositório.
        if (!page.StartsWith("https://github.com/" + Repo + "/", StringComparison.OrdinalIgnoreCase)) page = ReleasesPage;

        return latest > Current ? new UpdateInfo(latest, tag, page) : null;
    }

    /// <summary>"v0.4.0" → 0.4.0. Tags com sufixo (ex.: "v0.5.0-beta") são ignoradas.</summary>
    internal static bool TryParseTag(string tag, out Version version)
    {
        version = new Version(0, 0, 0);
        var s = tag.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(s, out var v)) return false;
        version = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        return true;
    }
}
