namespace Lasagna.Server.Rendering;

internal static class PageLinks
{
    public static string Item(string name)
        => $"/items/{Uri.EscapeDataString(name)}";

    public static string Bundle(string name)
        => $"/bundles/{Uri.EscapeDataString(name)}";

    public static string Kit(string name)
        => $"/kits/{Uri.EscapeDataString(name)}";

    public static string File(string itemName, string relativePath)
    {
        var segments = relativePath
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);

        return $"/items/{Uri.EscapeDataString(itemName)}/files/{string.Join("/", segments)}";
    }

    public static string RawFile(string itemName, string relativePath)
    {
        var segments = relativePath
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);

        return $"/files/{Uri.EscapeDataString(itemName)}/{string.Join("/", segments)}";
    }

    public static string KitFile(string kitName, string relativePath)
    {
        var segments = relativePath
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);

        return $"/kits/{Uri.EscapeDataString(kitName)}/files/{string.Join("/", segments)}";
    }

    public static string RawKitFile(string kitName, string relativePath)
    {
        var segments = relativePath
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);

        return $"/files/kit/{Uri.EscapeDataString(kitName)}/{string.Join("/", segments)}";
    }
}
