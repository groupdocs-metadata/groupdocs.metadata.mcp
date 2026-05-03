using System.ComponentModel;
using System.Text.Json;
using GroupDocs.Metadata.Options;
using GroupDocs.Mcp.Core;
using GroupDocs.Mcp.Core.Licensing;
using ModelContextProtocol.Server;

namespace GroupDocs.Metadata.Mcp.Tools;

[McpServerToolType]
public static class ReadMetadataTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool, Description(
        "Reads all metadata properties from a document (author, title, creation date, custom properties) and returns them as JSON. " +
        "Call this tool immediately whenever the user asks to read metadata, show document properties, or get author/title/date info. " +
        "Do NOT pre-check whether files exist — just pass the filename the user provided. " +
        "The tool resolves files from storage and returns an error with available files if a name is not found.")]
    public static async Task<string> ReadMetadata(
        IFileResolver resolver,
        ILicenseManager licenseManager,
        OutputHelper output,
        FileInput file,
        [Description("Password for protected documents")] string? password = null)
    {
        licenseManager.SetLicense();
        using var resolved = await resolver.ResolveAsync(file);

        var tempInput = Path.Combine(Path.GetTempPath(), $"gd_mcp_{Guid.NewGuid()}{Path.GetExtension(resolved.FileName)}");
        try
        {
            await using (var fs = File.Create(tempInput))
                await resolved.Stream.CopyToAsync(fs);

            var loadOptions = password != null ? new LoadOptions { Password = password } : null;
            using var metadata = loadOptions != null
                ? new Metadata(tempInput, loadOptions)
                : new Metadata(tempInput);

            var info = metadata.GetDocumentInfo();
            var properties = metadata.FindProperties(_ => true);

            var grouped = properties
                .GroupBy(p => p.Descriptor?.Name ?? "Unknown")
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => new { name = p.Name, value = p.Value?.RawValue }).ToList());

            var result = new
            {
                fileFormat = info.FileType.FileFormat.ToString(),
                mimeType = info.FileType.MimeType,
                pageCount = info.PageCount,
                sizeBytes = info.Size,
                isEncrypted = info.IsEncrypted,
                properties = grouped
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        finally
        {
            if (File.Exists(tempInput)) File.Delete(tempInput);
        }
    }
}
