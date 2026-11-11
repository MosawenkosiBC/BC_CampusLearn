using PdfSharp.Fonts;

namespace BC_CampusLearn.Services.Pdf;

internal static class EmbeddedPdfFont
{
    private static readonly Lazy<bool> Configuration = new(() =>
    {
        GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
        return true;
    });

    public static void EnsureConfigured() => _ = Configuration.Value;

    private sealed class EmbeddedFontResolver : IFontResolver
    {
        public byte[] GetFont(string faceName)
        {
            using Stream stream = typeof(EmbeddedPdfFont).Assembly.GetManifestResourceStream(
                "BC_CampusLearn.Assets.Fonts.DejaVuSans.ttf")
                ?? throw new InvalidOperationException("The embedded PDF font was not found.");
            using MemoryStream buffer = new();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new("DejaVuSans");
    }
}
