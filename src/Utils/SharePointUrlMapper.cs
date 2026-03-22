namespace AiAssistant.Api.Utils;

public static class SharePointUrlMapper
{
    private const string BlobPrefix = "https://oslometaiassistent.blob.core.windows.net/dev/mds/";
    private const string SharePointHost = "https://mesta.sharepoint.com";
    private const string SharePointLibraryPage =
        "/sites/OsloMet-Mesta-AIAssistent/Delte%20dokumenter/Forms/AllItems.aspx";
    private const string SharePointRoot =
        "/sites/OsloMet-Mesta-AIAssistent/Delte dokumenter/Generelt/AI Assistent prosjekt/Data";
    private const string ViewId = "1b40ae31-0c5e-4c08-9e2e-cb90cfa8103f";

    public static string? ToSharePointUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        if (rawUrl.StartsWith("https://mesta.sharepoint.com/", StringComparison.OrdinalIgnoreCase))
            return rawUrl;

        if (!rawUrl.StartsWith(BlobPrefix, StringComparison.OrdinalIgnoreCase))
            return rawUrl;

        var relativePath = rawUrl.Substring(BlobPrefix.Length);
        relativePath = Uri.UnescapeDataString(relativePath).TrimStart('/');

        var fullPath = $"{SharePointRoot}/{relativePath}".Replace("\\", "/");
        var lastSlash = fullPath.LastIndexOf('/');
        var parentPath = lastSlash > 0 ? fullPath[..lastSlash] : SharePointRoot;

        var encodedViewId = Uri.EscapeDataString(ViewId);
        var encodedId = Uri.EscapeDataString(fullPath);
        var encodedParent = Uri.EscapeDataString(parentPath);

        return $"{SharePointHost}{SharePointLibraryPage}?viewid={encodedViewId}&id={encodedId}&parent={encodedParent}";
    }
}