namespace ObsTelegramOverlay.Presentation.Templating;

public sealed class OverlayTemplateProvider
{
    private const string DefaultTemplateResourceSuffix = "Templates.default-overlay-template.html";

    public async Task<string> GetOverlayHtmlAsync(string? customTemplatePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customTemplatePath))
        {
            return await LoadEmbeddedDefaultTemplateAsync(cancellationToken);
        }

        if (!File.Exists(customTemplatePath))
        {
            throw new FileNotFoundException("Overlay template was not found.", customTemplatePath);
        }

        return await File.ReadAllTextAsync(customTemplatePath, cancellationToken);
    }

    private static async Task<string> LoadEmbeddedDefaultTemplateAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(OverlayTemplateProvider).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(static name => name.EndsWith(DefaultTemplateResourceSuffix, StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidOperationException("Embedded default overlay template resource was not found.");
        }

        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Failed to open embedded default overlay template resource stream.");

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
