using System.Globalization;
using BC_CampusLearn.Services.Pdf;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace BC_CampusLearn.Pages.Administrator.Settings;

internal static class SurveyResultsPdf
{
    public static byte[] Create(SurveyResultsModel report, CancellationToken cancellationToken)
    {
        EmbeddedPdfFont.EnsureConfigured();
        using var document = new PdfDocument();
        document.Info.Title = report.SurveyTitle;
        using (var writer = new ReportWriter(document))
        {
            writer.Write(report, cancellationToken);
        }

        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private sealed class ReportWriter(PdfDocument document) : IDisposable
    {
        private const double Margin = 42;
        private static readonly XSolidBrush Ink = new(XColor.FromArgb(37, 40, 48));
        private static readonly XSolidBrush Muted = new(XColor.FromArgb(92, 96, 105));
        private static readonly XSolidBrush Blue = new(XColor.FromArgb(99, 118, 237));
        private static readonly XSolidBrush Panel = new(XColor.FromArgb(244, 246, 252));
        private readonly XFont _titleFont = new("DejaVu Sans", 24);
        private readonly XFont _headingFont = new("DejaVu Sans", 13);
        private readonly XFont _bodyFont = new("DejaVu Sans", 10);
        private readonly XFont _smallFont = new("DejaVu Sans", 8.5);
        private XGraphics? _graphics;
        private double _pageHeight;
        private double _width;
        private double _y;

        private XGraphics Graphics => _graphics!;

        public void Write(SurveyResultsModel report, CancellationToken cancellationToken)
        {
            StartPage();
            WriteText("SURVEY RESULTS", _smallFont, Blue);
            _y += 5;
            WriteText(report.SurveyTitle, _titleFont, Ink);
            _y += 16;
            WriteText($"Responses overview  |  {report.SurveyStatus}", _headingFont, Ink);
            _y += 8;
            WriteMetrics(report);

            for (int index = 0; index < report.Questions.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var question = report.Questions[index];
                EnsureSpace(question.IsChoice && question.ResponseCount > 0 ? 158 : 66);
                _y += 14;
                Graphics.DrawLine(new XPen(XColor.FromArgb(232, 235, 242)), Margin, _y, Margin + _width, _y);
                _y += 13;
                WriteText($"{index + 1}. {question.Prompt}", _headingFont, Ink);
                _y += 8;
                if (question.IsChoice)
                    WriteChoices(question.ChartRows);
                else if (question.IsRating)
                    WriteRating(question);
                else
                    WriteLatest(question);
            }
        }

        private void WriteMetrics(SurveyResultsModel report)
        {
            (string Label, string Value)[] metrics =
            [
                ("Responses", report.ResponseCount.ToString(CultureInfo.InvariantCulture)),
                ("Average time", "Unavailable"),
                ("Duration", $"{report.DurationDays} {(report.DurationDays == 1 ? "day" : "days")}")
            ];
            double cardWidth = (_width - 20) / metrics.Length;
            EnsureSpace(60);
            for (int index = 0; index < metrics.Length; index++)
            {
                double x = Margin + index * (cardWidth + 10);
                Graphics.DrawRoundedRectangle(Panel, x, _y, cardWidth, 54, 8, 8);
                Graphics.DrawString(metrics[index].Label, _smallFont, Muted, x + 12, _y + 17);
                Graphics.DrawString(metrics[index].Value, _headingFont, Ink, x + 12, _y + 39);
            }
            _y += 64;
        }

        private void WriteChoices(IReadOnlyList<SurveyResultsModel.ChartRow> rows)
        {
            int total = rows.Sum(row => row.Count);
            if (total > 0)
            {
                const double diameter = 96;
                EnsureSpace(diameter + 12);
                double x = Margin + (_width - diameter) / 2;
                double angle = -90;
                foreach (var row in rows.Where(row => row.Count > 0))
                {
                    double sweep = 360.0 * row.Count / total;
                    var brush = ChartBrush(row.Color);
                    if (row.Count == total)
                        Graphics.DrawEllipse(brush, x, _y, diameter, diameter);
                    else
                        Graphics.DrawPie(brush, x, _y, diameter, diameter, angle, sweep);
                    angle += sweep;
                }
                Graphics.DrawEllipse(XBrushes.White, x + 10, _y + 10, diameter - 20, diameter - 20);
                _y += diameter + 12;
            }
            else
            {
                WriteText("No responses submitted", _bodyFont, Muted);
            }

            foreach (var row in rows)
            {
                double percent = total == 0 ? 0 : 100.0 * row.Count / total;
                EnsureSpace(_bodyFont.GetHeight() * 1.18);
                Graphics.DrawEllipse(ChartBrush(row.Color), Margin, _y + 4, 6, 6);
                WriteText($"{row.Label}  |  {row.Count} ({percent:0.#}%)", _bodyFont, Ink, indent: 12);
            }
        }

        private static XSolidBrush ChartBrush(string color)
        {
            uint rgb = uint.Parse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new XSolidBrush(XColor.FromArgb(0xff000000u | rgb));
        }

        private void WriteRating(SurveyResultsModel.QuestionResult question)
        {
            string average = question.AverageRating?.ToString("0.00") ?? "Unavailable";
            WriteText($"Average rating  |  {average}", _headingFont, Ink);
            var rows = question.ChartRows;
            int maxCount = rows.Max(row => row.Count);
            foreach (var row in rows)
            {
                EnsureSpace(18);
                Graphics.DrawString($"Level {row.Label}", _bodyFont, Muted, Margin, _y + 11);
                if (row.Count > 0)
                    Graphics.DrawRectangle(Blue, Margin + 64, _y + 4, (_width - 96) * row.Count / maxCount, 7);
                Graphics.DrawString(row.Count.ToString(CultureInfo.InvariantCulture), _bodyFont, Ink, Margin + _width - 25, _y + 11);
                _y += 18;
            }
        }

        private void WriteLatest(SurveyResultsModel.QuestionResult question)
        {
            if (question.Latest.Count == 0)
            {
                WriteText("No responses submitted", _bodyFont, Muted);
                return;
            }

            WriteText($"{question.ResponseCount} {(question.ResponseCount == 1 ? "response" : "responses")}  |  Latest responses", _smallFont, Muted);
            foreach (string answer in question.Latest)
            {
                _y += 4;
                WriteText(answer, _bodyFont, Ink);
            }
        }

        private void WriteText(string text, XFont font, XBrush brush, double indent = 0)
        {
            double lineHeight = font.GetHeight() * 1.18;
            foreach (string line in Wrap(text, font, _width - indent))
            {
                EnsureSpace(lineHeight);
                Graphics.DrawString(line, font, brush, new XPoint(Margin + indent, _y + font.GetHeight() * .82));
                _y += lineHeight;
            }
        }

        private IEnumerable<string> Wrap(string text, XFont font, double width)
        {
            foreach (string paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ').Split('\n'))
            {
                string line = string.Empty;
                foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate = line.Length == 0 ? word : $"{line} {word}";
                    if (Graphics.MeasureString(candidate, font).Width <= width)
                    {
                        line = candidate;
                        continue;
                    }

                    if (line.Length > 0)
                        yield return line;

                    line = string.Empty;
                    var characters = StringInfo.GetTextElementEnumerator(word);
                    while (characters.MoveNext())
                    {
                        string character = characters.GetTextElement();
                        if (line.Length > 0 && Graphics.MeasureString(line + character, font).Width > width)
                        {
                            yield return line;
                            line = string.Empty;
                        }
                        line += character;
                    }
                }
                yield return line;
            }
        }

        private void EnsureSpace(double height)
        {
            if (_y + height > _pageHeight - Margin)
                StartPage();
        }

        private void StartPage()
        {
            Dispose();
            PdfPage page = document.AddPage();
            page.Size = PageSize.A4;
            _graphics = XGraphics.FromPdfPage(page);
            _pageHeight = page.Height.Point;
            _width = page.Width.Point - 2 * Margin;
            _y = Margin;
        }

        public void Dispose()
        {
            if (_graphics is null) return;

            Graphics.DrawString($"Mzala Connect  |  Survey results  |  Page {document.PageCount}", _smallFont, Muted, Margin, _pageHeight - 22);
            _graphics.Dispose();
            _graphics = null;
        }
    }
}
