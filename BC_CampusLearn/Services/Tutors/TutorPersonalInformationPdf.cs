using System.Reflection;
using BC_CampusLearn.Models.Entities;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace BC_CampusLearn.Services.Tutors;

public sealed record TutorPersonalInformationPdfData(
    TutorPersonalInformation PersonalInformation,
    TutorBankingDetails BankingDetails,
    string EmailAddress,
    string CellNumber);

public interface ITutorPersonalInformationPdf
{
    byte[] Create(TutorPersonalInformationPdfData data);
}

public sealed class TutorPersonalInformationPdf : ITutorPersonalInformationPdf
{
    private const string TemplateResource =
        "BC_CampusLearn.Assets.Documents.HR Personal Info Form Tutors.pdf";

    private static readonly object FontLock = new();
    private static bool _fontConfigured;

    public TutorPersonalInformationPdf()
    {
        EnsureFontResolver();
    }

    internal static string BuildFileName(string? studentNumber, string fullName)
    {
        static string Safe(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string cleaned = new(value
                .Where(character => !invalid.Contains(character))
                .ToArray());
            return string.Join('_', cleaned.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries));
        }

        string number = Safe(studentNumber ?? "Tutor");
        string name = Safe(fullName);
        return $"HR_Personal_Info_{number}_{name}.pdf";
    }

    public byte[] Create(TutorPersonalInformationPdfData data)
    {
        using Stream template = Resource(TemplateResource);
        using PdfDocument document = PdfReader.Open(template, PdfDocumentOpenMode.Modify);
        while (document.PageCount > 1)
        {
            document.Pages.RemoveAt(document.PageCount - 1);
        }

        PdfPage page = document.Pages[0];
        // The supplied Word-generated template has a malformed, empty AcroForm.
        // The completed document is intentionally static, so remove those widgets
        // before adding the tutor's values.
        document.Internals.Catalog.Elements.Remove("/AcroForm");
        page.Elements.Remove("/Annots");
        using XGraphics graphics = XGraphics.FromPdfPage(
            page,
            XGraphicsPdfPageOptions.Append);
        XFont regular = new("DejaVu Sans", 9, XFontStyleEx.Regular);
        XFont small = new("DejaVu Sans", 7.5, XFontStyleEx.Regular);
        TutorPersonalInformation info = data.PersonalInformation;
        TutorBankingDetails banking = data.BankingDetails;

        Draw(graphics, regular, info.Surname, 218, 146, 354, 39);
        Draw(graphics, regular, info.FullNames, 218, 185, 354, 35);
        Draw(graphics, small,
            string.Join("\n",
                info.StreetAddress,
                $"{info.Suburb}, {info.City}",
                $"{info.Province}, {info.Country}, {info.PostalCode}"),
            218, 220, 354, 50);
        Draw(graphics, regular, info.IncomeTaxNumber, 218, 270, 354, 38);
        Draw(graphics, small, info.HomeLanguage, 218, 308, 176, 29);
        Draw(graphics, small, data.CellNumber, 474, 308, 98, 29);
        Draw(graphics, small, info.Gender, 218, 337, 176, 28);
        Draw(graphics, small, info.Nationality, 474, 337, 98, 28);
        Draw(graphics, small, info.IdentityOrPassportNumber, 218, 365, 176, 35);
        Draw(graphics, small,
            info.WorkPermitExpiryDate?.ToString("dd/MM/yyyy") ?? "N/A",
            474, 365, 98, 35);
        Draw(graphics, small, info.BirthDate.ToString("dd/MM/yyyy"), 218, 400, 176, 29);
        Draw(graphics, small, info.AsylumNumber ?? "N/A", 474, 400, 98, 29);
        Draw(graphics, regular, data.EmailAddress, 218, 429, 354, 32);
        Draw(graphics, regular, info.EmergencyContactName, 226, 481, 346, 35);
        Draw(graphics, regular, info.EmergencyContactPrimaryPhone, 226, 516, 346, 35);
        Draw(graphics, regular, info.EmergencyContactRelationship, 226, 551, 346, 36);
        Draw(graphics, regular, banking.BankName, 226, 604, 346, 34);
        Draw(graphics, small, banking.RecipientAccountNumber, 226, 638, 153, 35);
        Draw(graphics, small, banking.AccountHolder, 465, 638, 107, 35);
        Draw(graphics, small, banking.AccountType, 226, 673, 346, 36);
        DrawSignature(graphics, small, info, 226, 709, 346, 50);
        Draw(graphics, regular,
            info.SignedAt?.ToLocalTime().ToString("dd/MM/yyyy") ?? string.Empty,
            226, 759, 346, 29);

        using MemoryStream output = new();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static void Draw(
        XGraphics graphics,
        XFont font,
        string value,
        double x,
        double y,
        double width,
        double height)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        string[] lines = Wrap(graphics, font, value.Trim(), width - 8, 3);
        double lineHeight = font.GetHeight() * 1.05;
        double totalHeight = lines.Length * lineHeight;
        double top = y + Math.Max(3, (height - totalHeight) / 2);
        for (int index = 0; index < lines.Length; index++)
        {
            graphics.DrawString(
                lines[index],
                font,
                XBrushes.Black,
                new XPoint(x + 4, top + ((index + 0.8) * lineHeight)));
        }
    }

    private static void DrawSignature(
        XGraphics graphics,
        XFont font,
        TutorPersonalInformation info,
        double x,
        double y,
        double width,
        double height)
    {
        if (info.SignatureImage is { Length: > 0 })
        {
            using MemoryStream stream = new(info.SignatureImage, writable: false);
            using XImage image = XImage.FromStream(stream);
            double availableWidth = width - 8;
            double availableHeight = height - 17;
            double scale = Math.Min(
                availableWidth / image.PixelWidth,
                availableHeight / image.PixelHeight);
            double imageWidth = image.PixelWidth * scale;
            double imageHeight = image.PixelHeight * scale;
            graphics.DrawImage(
                image,
                x + 4,
                y + 2 + ((availableHeight - imageHeight) / 2),
                imageWidth,
                imageHeight);
        }

        Draw(graphics, font, info.SignatureName ?? string.Empty, x, y + height - 16, width, 16);
    }

    private static string[] Wrap(
        XGraphics graphics,
        XFont font,
        string value,
        double width,
        int maximumLines)
    {
        List<string> lines = [];
        foreach (string paragraph in value.Replace("\r", string.Empty).Split('\n'))
        {
            string current = string.Empty;
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = current.Length == 0 ? word : $"{current} {word}";
                if (graphics.MeasureString(candidate, font).Width <= width)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0) lines.Add(current);
                current = word;
                if (lines.Count == maximumLines) break;
            }

            if (lines.Count < maximumLines && current.Length > 0) lines.Add(current);
            if (lines.Count == maximumLines) break;
        }

        if (lines.Count == 0) return [string.Empty];
        if (graphics.MeasureString(lines[^1], font).Width > width)
        {
            string last = lines[^1];
            while (last.Length > 1 &&
                graphics.MeasureString($"{last}...", font).Width > width)
            {
                last = last[..^1];
            }
            lines[^1] = $"{last}...";
        }
        return lines.ToArray();
    }

    private static void EnsureFontResolver()
    {
        if (_fontConfigured) return;
        lock (FontLock)
        {
            if (_fontConfigured) return;
            GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
            _fontConfigured = true;
        }
    }

    private static Stream Resource(string name) =>
        typeof(TutorPersonalInformationPdf).Assembly
            .GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Embedded PDF resource '{name}' was not found.");

    private sealed class EmbeddedFontResolver : IFontResolver
    {
        private const string FontResource =
            "BC_CampusLearn.Assets.Fonts.DejaVuSans.ttf";

        public byte[] GetFont(string faceName)
        {
            using Stream stream = Resource(FontResource);
            using MemoryStream buffer = new();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        public FontResolverInfo ResolveTypeface(
            string familyName,
            bool isBold,
            bool isItalic) => new("DejaVuSans");
    }
}
