using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using static BC_CampusLearn.Pages.Administrator.Admin.CompensationModel;

namespace BC_CampusLearn.Services.Compensation;

// A native Excel workbook, with no server-side Excel installation required.
public static class CompensationWorkbook
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static byte[] Create(DateOnly from, DateOnly to, string currency,
        IReadOnlyList<TutorEarnings> tutors, IReadOnlyList<EarningSession> sessions,
        DateTimeOffset generatedAt)
    {
        if (sessions.Any(session => session.Amount is null))
            throw new InvalidOperationException("Every exported session must have a payment amount.");
        var summary = Metadata("Tutor compensation", from, to, generatedAt);
        summary.Add(Row(6, Text("Approved sessions"), Number(sessions.Count)));
        summary.Add(Row(7, Text("Total tutors"), Number(tutors.Count)));
        summary.Add(Row(10, Headers("Number", "Tutor", "Personnel number", "Email", "Campus", "Approved sessions", $"Amount earned ({currency})")));
        int row = 11;
        foreach (var tutor in tutors)
            summary.Add(Row(row++, Number(tutor.TutorId), Text(tutor.Name), Text(tutor.PersonnelNumber),
                Text(tutor.Email), Text(tutor.Campus), Number(tutor.ApprovedSessions), Number(tutor.Earnings ?? 0m, 3)));
        summary.Add(Row(row + 2, Text("Total", 6), Text(""), Text(""), Text(""), Text(""),
            Formula(tutors.Count == 0 ? "0" : $"SUM(F11:F{row - 1})", sessions.Count),
            Formula(tutors.Count == 0 ? "0" : $"SUM(G11:G{row - 1})", sessions.Sum(session => session.Amount!.Value), 3)));

        var details = Metadata("Approved session details", from, to, generatedAt);
        details.Add(Row(10, Headers("Session ID", "Number", "Tutor", "Personnel number", "Campus", "Module", "Session date (SAST)", "Super admin approved (SAST)", $"Amount earned ({currency})")));
        row = 11;
        foreach (var session in sessions)
            details.Add(Row(row++, Number(session.BookingId), Number(session.TutorId), Text(session.TutorName),
                Text(session.PersonnelNumber), Text(session.Campus), Text(session.ModuleCode),
                Date(session.SessionDate.ToOffset(CampusOffset).DateTime, 5),
                Date(session.ApprovedAt.ToOffset(CampusOffset).DateTime, 5), Number(session.Amount!.Value, 3)));
        details.Add(Row(row + 2, Text("Total", 6), Text(""), Text(""), Text(""), Text(""), Text(""), Text(""), Text(""),
            Formula(sessions.Count == 0 ? "0" : $"SUM(I11:I{row - 1})", sessions.Sum(session => session.Amount!.Value), 3)));

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
            Write(zip, "[Content_Types].xml", new XElement(ct + "Types",
                new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                ContentType(ct, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                ContentType(ct, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"),
                ContentType(ct, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"),
                ContentType(ct, "/xl/worksheets/sheet2.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
            Write(zip, "_rels/.rels", Relationships(("rId1", "officeDocument", "xl/workbook.xml")));
            Write(zip, "xl/workbook.xml", new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", R),
                new XElement(S + "sheets", Sheet("Tutor earnings", 1), Sheet("Approved sessions", 2)),
                new XElement(S + "calcPr", new XAttribute("calcId", "191029"), new XAttribute("fullCalcOnLoad", "1"))));
            Write(zip, "xl/_rels/workbook.xml.rels", Relationships(
                ("rId1", "worksheet", "worksheets/sheet1.xml"), ("rId2", "worksheet", "worksheets/sheet2.xml"), ("rId3", "styles", "styles.xml")));
            Write(zip, "xl/styles.xml", Styles());
            Write(zip, "xl/worksheets/sheet1.xml", Worksheet(summary, [28, 28, 22, 36, 24, 20, 26], tutors.Count));
            Write(zip, "xl/worksheets/sheet2.xml", Worksheet(details, [28, 28, 28, 22, 24, 18, 28, 28, 26], sessions.Count));
        }
        return output.ToArray();
    }

    private static List<XElement> Metadata(string title, DateOnly from, DateOnly to, DateTimeOffset generatedAt) =>
    [
        Row(1, Text(title, 1)),
        Row(3, Text("Period start (inclusive)"), Date(from.ToDateTime(TimeOnly.MinValue), 4)),
        Row(4, Text("Period end (inclusive)"), Date(to.ToDateTime(TimeOnly.MinValue), 4)),
        Row(5, Text("Generated (SAST)"), Date(generatedAt.ToOffset(CampusOffset).DateTime, 5))
    ];

    private static XElement Text(string value, int style = 0) => new(S + "c", new XAttribute("t", "inlineStr"),
        new XAttribute("s", style), new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"),
            new string(value.Where(System.Xml.XmlConvert.IsXmlChar).ToArray()))));
    private static XElement Number(decimal value, int style = 0) => new(S + "c", new XAttribute("s", style),
        new XElement(S + "v", value.ToString(CultureInfo.InvariantCulture)));
    private static XElement Date(DateTime value, int style) => Number((decimal)value.ToOADate(), style);
    private static XElement Formula(string formula, decimal cachedValue, int style = 0)
    {
        var cell = Number(cachedValue, style);
        cell.AddFirst(new XElement(S + "f", formula));
        return cell;
    }
    private static XElement[] Headers(params string[] labels) => labels.Select(label => Text(label, 2)).ToArray();
    private static XElement Row(int index, params XElement[] cells)
    {
        for (int column = 0; column < cells.Length; column++)
            cells[column].SetAttributeValue("r", $"{(char)('A' + column)}{index}");
        return new XElement(S + "row", new XAttribute("r", index), cells);
    }
    private static XElement Sheet(string name, int id) => new(S + "sheet", new XAttribute("name", name),
        new XAttribute("sheetId", id), new XAttribute(R + "id", $"rId{id}"));
    private static XElement ContentType(XNamespace ns, string part, string type) => new(ns + "Override",
        new XAttribute("PartName", part), new XAttribute("ContentType", type));
    private static XElement Relationships(params (string Id, string Type, string Target)[] items)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new XElement(ns + "Relationships", items.Select(item => new XElement(ns + "Relationship",
            new XAttribute("Id", item.Id), new XAttribute("Type", R.NamespaceName + "/" + item.Type), new XAttribute("Target", item.Target))));
    }
    private static XElement Worksheet(List<XElement> rows, double[] widths, int count) => new(S + "worksheet",
        new XElement(S + "sheetViews", new XElement(S + "sheetView", new XAttribute("workbookViewId", "0"),
            new XElement(S + "pane", new XAttribute("ySplit", "10"), new XAttribute("topLeftCell", "A11"),
                new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
        new XElement(S + "cols", widths.Select((width, index) => new XElement(S + "col",
            new XAttribute("min", index + 1), new XAttribute("max", index + 1), new XAttribute("width", width), new XAttribute("customWidth", "1")))),
        new XElement(S + "sheetData", rows),
        new XElement(S + "autoFilter", new XAttribute("ref", $"A10:{(char)('A' + widths.Length - 1)}{10 + count}")),
        new XElement(S + "pageSetup", new XAttribute("orientation", "landscape"), new XAttribute("paperSize", "9")));

    private static XElement Styles()
    {
        XElement Font(bool bold, int size, string color) => new(S + "font", bold ? new XElement(S + "b") : null,
            new XElement(S + "sz", new XAttribute("val", size)), new XElement(S + "color", new XAttribute("rgb", color)),
            new XElement(S + "name", new XAttribute("val", "Calibri")));
        XElement Xf(int font, int fill, int numberFormat) => new(S + "xf", new XAttribute("fontId", font),
            new XAttribute("fillId", fill), new XAttribute("borderId", "0"), new XAttribute("numFmtId", numberFormat),
            new XAttribute("xfId", "0"), new XAttribute("applyNumberFormat", "1"), new XAttribute("applyFont", "1"), new XAttribute("applyFill", "1"));
        return new XElement(S + "styleSheet",
            new XElement(S + "numFmts", new XAttribute("count", "2"),
                new XElement(S + "numFmt", new XAttribute("numFmtId", "164"), new XAttribute("formatCode", "dd mmm yyyy")),
                new XElement(S + "numFmt", new XAttribute("numFmtId", "165"), new XAttribute("formatCode", "dd mmm yyyy hh:mm"))),
            new XElement(S + "fonts", new XAttribute("count", "3"), Font(false, 11, "FF303941"), Font(true, 18, "FF872E5A"), Font(true, 11, "FFFFFFFF")),
            new XElement(S + "fills", new XAttribute("count", "4"),
                new XElement(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", "none"))),
                new XElement(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", "gray125"))),
                new XElement(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", "solid"), new XElement(S + "fgColor", new XAttribute("rgb", "FF872E5A")), new XElement(S + "bgColor", new XAttribute("indexed", "64")))),
                new XElement(S + "fill", new XElement(S + "patternFill", new XAttribute("patternType", "solid"), new XElement(S + "fgColor", new XAttribute("rgb", "FF000000")), new XElement(S + "bgColor", new XAttribute("indexed", "64"))))),
            new XElement(S + "borders", new XAttribute("count", "1"), new XElement(S + "border", new XElement(S + "left"), new XElement(S + "right"), new XElement(S + "top"), new XElement(S + "bottom"), new XElement(S + "diagonal"))),
            new XElement(S + "cellStyleXfs", new XAttribute("count", "1"), Xf(0, 0, 0)),
            new XElement(S + "cellXfs", new XAttribute("count", "7"), Xf(0, 0, 0), Xf(1, 0, 0), Xf(2, 2, 0), Xf(0, 0, 4), Xf(0, 0, 164), Xf(0, 0, 165), Xf(2, 3, 0)),
            new XElement(S + "cellStyles", new XAttribute("count", "1"), new XElement(S + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", "0"), new XAttribute("builtinId", "0"))));
    }
    private static void Write(ZipArchive zip, string path, XElement root)
    {
        using var stream = zip.CreateEntry(path, CompressionLevel.Fastest).Open();
        new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).Save(stream);
    }
}
