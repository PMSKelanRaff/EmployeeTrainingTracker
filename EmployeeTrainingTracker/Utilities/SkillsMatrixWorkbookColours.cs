using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// PREVIEW ONLY - works out the colours each workbook cell DISPLAYS in Excel, so the preview can show
    /// the Skills Matrix's own colour standard exactly (level colours, INAB green/red, headings, key).
    ///
    /// Evaluates the sheet's conditional formatting the way Excel does (rules in priority order; the first
    /// matching rule that sets a fill wins; 'contains text' and 2/3-colour scales supported), falling back
    /// to the cell's fixed fill. Theme colours and tints are resolved from the workbook's own theme.
    /// Read-only: works from the in-memory package and the bytes already read; nothing is written.
    /// Verified cell-by-cell against Excel's own DisplayFormat colours for the verified copy.
    /// </summary>
    public sealed class SkillsMatrixWorkbookColours
    {
        /// <summary>
        /// Fill = the colour to paint (for patterned fills, the visual blend of pattern colour over background);
        /// Pattern = the OOXML pattern name ("solid", "mediumGray", ... or null for none);
        /// PatternColor / PatternBack = the pattern's foreground and background as stored in the workbook.
        /// Provenance (matches SkillMatrixCellFormats): FillSource = "None" | "CellStyle" | "ConditionalFormat",
        /// ConditionalRule = the rule that set the fill; FontSource = "CellStyle" | "ConditionalFormat",
        /// FontRule = the rule that set the font colour.
        /// </summary>
        public sealed record CellDisplay(Color? Fill, Color Font, bool Bold, string? Pattern = null,
                                         Color? PatternColor = null, Color? PatternBack = null,
                                         string FillSource = "None", string? ConditionalRule = null,
                                         string FontSource = "CellStyle", string? FontRule = null);

        private sealed record Rule(int Priority, List<(int R1, int C1, int R2, int C2)> Ranges, string Type,
                                   string? Text, Color? DxfFill, Color? DxfFont, List<(string Type, double? Val, Color Color)>? Scale,
                                   string Sqref)
        {
            /// <summary>The rule as written, e.g. 'priority 3: containsText "4" on E5:E18'.</summary>
            public string Describe() => $"priority {Priority}: {Type}{(Text != null ? $" \"{Text}\"" : "")} on {Sqref}";
        }

        private readonly ExcelWorksheet _ws;
        private readonly Color[] _theme;
        private readonly List<Rule> _rules;
        private readonly Dictionary<Rule, (double[] Sorted, double Min, double Max)> _scaleStats = new();

        private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private SkillsMatrixWorkbookColours(ExcelWorksheet ws, Color[] theme, List<Rule> rules)
        {
            _ws = ws;
            _theme = theme;
            _rules = rules;
        }

        /// <summary>Builds the colour evaluator for one sheet (theme read from the workbook bytes).</summary>
        public static SkillsMatrixWorkbookColours For(ExcelWorksheet ws, ExcelPackage package, byte[] workbookBytes)
        {
            // Conditional formatting and dxf styles are read from the original file bytes: EPPlus moves
            // them out of its in-memory XML when it loads a workbook.
            var theme = ReadTheme(workbookBytes);
            var dxfs = ReadDxfs(LoadPart(workbookBytes, "xl/styles.xml") ?? package.Workbook.StylesXml, theme);
            var sheetXml = LoadPart(workbookBytes, SheetPartPath(workbookBytes, ws.Name)) ?? ws.WorksheetXml;
            var rules = ReadRules(sheetXml, dxfs, theme);
            return new SkillsMatrixWorkbookColours(ws, theme, rules);
        }

        private static XmlDocument? LoadPart(byte[] workbookBytes, string? partPath)
        {
            if (partPath == null) return null;
            using var zip = new ZipArchive(new MemoryStream(workbookBytes, writable: false), ZipArchiveMode.Read);
            var entry = zip.GetEntry(partPath);
            if (entry == null) return null;
            var doc = new XmlDocument { XmlResolver = null };
            using var s = entry.Open();
            doc.Load(s);
            return doc;
        }

        // Sheet name -> part path, via xl/workbook.xml and its relationships
        private static string? SheetPartPath(byte[] workbookBytes, string sheetName)
        {
            var wb = LoadPart(workbookBytes, "xl/workbook.xml");
            var rels = LoadPart(workbookBytes, "xl/_rels/workbook.xml.rels");
            if (wb == null || rels == null) return null;

            var ns = new XmlNamespaceManager(wb.NameTable);
            ns.AddNamespace("m", MainNs);
            var sheet = wb.SelectNodes("//m:sheets/m:sheet", ns)!.Cast<XmlElement>().FirstOrDefault(e => e.GetAttribute("name") == sheetName);
            string? rid = sheet?.GetAttribute("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            if (string.IsNullOrEmpty(rid)) return null;

            var rns = new XmlNamespaceManager(rels.NameTable);
            rns.AddNamespace("r", "http://schemas.openxmlformats.org/package/2006/relationships");
            var rel = rels.SelectNodes("//r:Relationship", rns)!.Cast<XmlElement>().FirstOrDefault(e => e.GetAttribute("Id") == rid);
            string? target = rel?.GetAttribute("Target");
            if (string.IsNullOrEmpty(target)) return null;
            return target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
        }

        /// <summary>The fill and font colour Excel displays for the cell.</summary>
        public CellDisplay Display(int r, int c)
        {
            // Evaluated per cell, like Excel's DisplayFormat. (For a merged area the preview always asks for
            // the top-left cell, which is the one Excel paints the whole area with.)
            var cell = _ws.Cells[r, c];
            Color font = ResolveColor(cell.Style.Font.Color) ?? Color.Black;
            bool bold = cell.Style.Font.Bold;

            Color? fill = null;
            Rule? fillRule = null, fontRule = null;
            foreach (var rule in _rules.Where(x => x.Ranges.Any(g => r >= g.R1 && r <= g.R2 && c >= g.C1 && c <= g.C2)))
            {
                if (rule.Type == "containsText")
                {
                    if (rule.Text == null || !ContainsText(cell, rule.Text)) continue;
                    if (fontRule == null && rule.DxfFont.HasValue) { font = rule.DxfFont.Value; fontRule = rule; }
                    if (fill == null && rule.DxfFill.HasValue) { fill = rule.DxfFill; fillRule = rule; }
                }
                else if (rule.Type == "colorScale" && rule.Scale != null)
                {
                    if (fill == null && NumericValue(cell) is double v) { fill = ScaleColor(rule, v); fillRule = rule; }
                }
                if (fill != null && fontRule != null) break; // each property comes from the highest-priority rule that sets it
            }
            string fontSource = fontRule != null ? "ConditionalFormat" : "CellStyle";
            string? fontRuleText = fontRule?.Describe();

            if (fill != null)
                return new CellDisplay(fill, font, bold, "solid", fill, null,
                                       "ConditionalFormat", fillRule!.Describe(), fontSource, fontRuleText);

            // Fixed fill. Solid = its colour. Patterned (e.g. the 50% grey dots on category rows) = the
            // visual blend of the pattern colour over its background, since a grid cell can't draw the dots.
            var pattern = cell.Style.Fill.PatternType;
            if (pattern == ExcelFillStyle.None)
                return new CellDisplay(null, font, bold, FontSource: fontSource, FontRule: fontRuleText);

            Color fore = ResolveColor(cell.Style.Fill.BackgroundColor) ?? Color.Black;   // EPPlus: pattern colour (fgColor)
            if (pattern == ExcelFillStyle.Solid)
                return new CellDisplay(fore, font, bold, "solid", fore, null, "CellStyle", null, fontSource, fontRuleText);

            // EPPlus: background (bgColor). 'Automatic' (indexed 64) means the window background, i.e. white.
            var bgColor = cell.Style.Fill.PatternColor;
            Color back = bgColor.Indexed == 64 && string.IsNullOrEmpty(bgColor.Rgb) && !bgColor.Theme.HasValue
                ? Color.White
                : ResolveColor(bgColor) ?? Color.White;
            double coverage = pattern switch
            {
                ExcelFillStyle.DarkGray => 0.75,
                ExcelFillStyle.MediumGray => 0.50,
                ExcelFillStyle.LightGray => 0.25,
                ExcelFillStyle.Gray125 => 0.125,
                ExcelFillStyle.Gray0625 => 0.0625,
                _ => 0.50,
            };
            var blend = Color.FromArgb(Lerp(back.R, fore.R, coverage), Lerp(back.G, fore.G, coverage), Lerp(back.B, fore.B, coverage));
            return new CellDisplay(blend, font, bold, OoxmlPatternName(pattern), fore, back, "CellStyle", null, fontSource, fontRuleText);
        }

        // EPPlus enum name -> the OOXML patternType name used in the workbook ('MediumGray' -> 'mediumGray')
        private static string OoxmlPatternName(ExcelFillStyle pattern)
        {
            string name = pattern.ToString();
            return char.ToLowerInvariant(name[0]) + name[1..];
        }

        // ---------------------------------------------------------------------------------------------
        // Rule evaluation

        private static bool ContainsText(ExcelRange cell, string text)
        {
            // Excel: NOT(ISERROR(SEARCH(text, cell))) - case-insensitive search in the cell's value as text
            object? v = cell.Value;
            if (v == null) return false;
            string s = v is double d ? d.ToString(CultureInfo.InvariantCulture)
                     : v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture)
                     : v.ToString() ?? "";
            return s.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static double? NumericValue(ExcelRange cell) =>
            cell.Value is double or int or decimal ? Convert.ToDouble(cell.Value) : null;

        private Color ScaleColor(Rule rule, double v)
        {
            if (!_scaleStats.TryGetValue(rule, out var stats))
            {
                var values = new List<double>();
                foreach (var g in rule.Ranges)
                    for (int r = g.R1; r <= g.R2; r++)
                        for (int c = g.C1; c <= g.C2; c++)
                            if (NumericValue(_ws.Cells[r, c]) is double x) values.Add(x);
                values.Sort();
                stats = (values.ToArray(), values.Count > 0 ? values[0] : 0, values.Count > 0 ? values[^1] : 0);
                _scaleStats[rule] = stats;
            }

            var points = rule.Scale!.Select(p => (Value: Threshold(p, stats), p.Color)).ToList();
            var (lowV, lowC) = points[0];
            var (highV, highC) = points[^1];
            if (v >= highV) return highC;
            if (v <= lowV) return lowC;
            for (int i = 0; i < points.Count - 1; i++)
            {
                var (aV, aC) = points[i];
                var (bV, bC) = points[i + 1];
                if (v >= aV && v <= bV)
                {
                    double t = bV > aV ? (v - aV) / (bV - aV) : 1;
                    return Color.FromArgb(Lerp(aC.R, bC.R, t), Lerp(aC.G, bC.G, t), Lerp(aC.B, bC.B, t));
                }
            }
            return highC;
        }

        private static int Lerp(int a, int b, double t) => (int)Math.Round(a + (b - a) * t);

        private static double Threshold((string Type, double? Val, Color Color) p, (double[] Sorted, double Min, double Max) s) =>
            p.Type switch
            {
                "min" => s.Min,
                "max" => s.Max,
                "num" => p.Val ?? 0,
                "percent" => s.Min + (s.Max - s.Min) * (p.Val ?? 0) / 100.0,
                "percentile" => Percentile(s.Sorted, (p.Val ?? 0) / 100.0),
                _ => s.Min,
            };

        // Excel PERCENTILE.INC
        private static double Percentile(double[] sorted, double k)
        {
            if (sorted.Length == 0) return 0;
            double pos = (sorted.Length - 1) * k;
            int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
            return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
        }

        // ---------------------------------------------------------------------------------------------
        // Reading theme, dxf styles and rules (raw XML, read-only)

        private static Color[] ReadTheme(byte[] workbookBytes)
        {
            // Excel theme index order: lt1, dk1, lt2, dk2, accent1..6, hlink, folHlink
            var defaults = new[] { "FFFFFF", "000000", "E7E6E6", "44546A", "5B9BD5", "ED7D31", "A5A5A5", "FFC000", "4472C4", "70AD47", "0563C1", "954F72" };
            var result = defaults.Select(Hex).ToArray();
            try
            {
                using var zip = new ZipArchive(new MemoryStream(workbookBytes, writable: false), ZipArchiveMode.Read);
                var entry = zip.GetEntry("xl/theme/theme1.xml");
                if (entry == null) return result;
                var doc = new XmlDocument { XmlResolver = null };
                using (var s = entry.Open()) doc.Load(s);
                var ns = new XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("a", "http://schemas.openxmlformats.org/drawingml/2006/main");
                var scheme = doc.SelectSingleNode("//a:clrScheme", ns);
                if (scheme == null) return result;
                string[] order = { "lt1", "dk1", "lt2", "dk2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hlink", "folHlink" };
                for (int i = 0; i < order.Length; i++)
                {
                    var node = scheme.SelectSingleNode($"a:{order[i]}", ns);
                    string? val = node?.SelectSingleNode("a:srgbClr/@val", ns)?.Value
                               ?? node?.SelectSingleNode("a:sysClr/@lastClr", ns)?.Value;
                    if (val != null) result[i] = Hex(val);
                }
            }
            catch
            {
                // fall back to the Office default theme
            }
            return result;
        }

        private static List<(Color? Fill, Color? Font)> ReadDxfs(XmlDocument styles, Color[] theme)
        {
            var list = new List<(Color?, Color?)>();
            var ns = new XmlNamespaceManager(styles.NameTable);
            ns.AddNamespace("m", MainNs);
            foreach (XmlElement dxf in styles.SelectNodes("//m:dxfs/m:dxf", ns)!)
            {
                // dxf solid fills use bgColor (fgColor as a fallback)
                var fillNode = dxf.SelectSingleNode("m:fill/m:patternFill/m:bgColor", ns) as XmlElement
                            ?? dxf.SelectSingleNode("m:fill/m:patternFill/m:fgColor", ns) as XmlElement;
                var fontNode = dxf.SelectSingleNode("m:font/m:color", ns) as XmlElement;
                list.Add((ColorFromXml(fillNode, theme), ColorFromXml(fontNode, theme)));
            }
            return list;
        }

        private static List<Rule> ReadRules(XmlDocument sheet, List<(Color? Fill, Color? Font)> dxfs, Color[] theme)
        {
            var rules = new List<Rule>();
            var ns = new XmlNamespaceManager(sheet.NameTable);
            ns.AddNamespace("m", MainNs);
            foreach (XmlElement cf in sheet.SelectNodes("//m:conditionalFormatting", ns)!)
            {
                string sqref = cf.GetAttribute("sqref");
                var ranges = sqref.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(ParseRange).ToList();
                foreach (XmlElement rule in cf.SelectNodes("m:cfRule", ns)!)
                {
                    int priority = int.TryParse(rule.GetAttribute("priority"), out var p) ? p : int.MaxValue;
                    string type = rule.GetAttribute("type");
                    Color? dxfFill = null, dxfFont = null;
                    if (int.TryParse(rule.GetAttribute("dxfId"), out var dxfId) && dxfId >= 0 && dxfId < dxfs.Count)
                        (dxfFill, dxfFont) = dxfs[dxfId];

                    List<(string, double?, Color)>? scale = null;
                    if (type == "colorScale")
                    {
                        var cfvos = rule.SelectNodes("m:colorScale/m:cfvo", ns)!.Cast<XmlElement>().ToList();
                        var colors = rule.SelectNodes("m:colorScale/m:color", ns)!.Cast<XmlElement>().ToList();
                        scale = cfvos.Zip(colors, (v, col) => (
                                v.GetAttribute("type"),
                                double.TryParse(v.GetAttribute("val"), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : (double?)null,
                                ColorFromXml(col, theme) ?? Color.White))
                            .ToList();
                    }

                    string? text = rule.HasAttribute("text") ? rule.GetAttribute("text") : null;
                    rules.Add(new Rule(priority, ranges, type, text, dxfFill, dxfFont, scale, sqref));
                }
            }
            return rules.OrderBy(r => r.Priority).ToList();
        }

        // ---------------------------------------------------------------------------------------------
        // Colours

        private Color? ResolveColor(ExcelColor color)
        {
            if (!string.IsNullOrEmpty(color.Rgb))
                return Hex(color.Rgb.Length == 8 ? color.Rgb[2..] : color.Rgb);
            if (color.Theme.HasValue)
                return ApplyTint(_theme[Math.Clamp((int)color.Theme.Value, 0, _theme.Length - 1)], (double)color.Tint);
            if (color.Indexed >= 0)
                return Indexed(color.Indexed);
            return null;
        }

        private static Color? ColorFromXml(XmlElement? node, Color[] theme)
        {
            if (node == null) return null;
            double tint = double.TryParse(node.GetAttribute("tint"), NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : 0;
            if (node.HasAttribute("rgb"))
            {
                string rgb = node.GetAttribute("rgb");
                return ApplyTint(Hex(rgb.Length == 8 ? rgb[2..] : rgb), tint);
            }
            if (node.HasAttribute("theme") && int.TryParse(node.GetAttribute("theme"), out var th) && th >= 0 && th < theme.Length)
                return ApplyTint(theme[th], tint);
            if (node.HasAttribute("indexed") && int.TryParse(node.GetAttribute("indexed"), out var ix))
                return Indexed(ix);
            return null;
        }

        // Excel's tint: lighten (tint > 0) or darken (tint < 0) the luminance in HLS space. Excel uses the
        // Windows shell colour routines (HLS on a 0-240 scale), so those are used here to match it exactly.
        private static Color ApplyTint(Color c, double tint)
        {
            if (Math.Abs(tint) < 1e-9) return c;
            try
            {
                const double HlsMax = 240;
                ColorRGBToHLS(ColorTranslator.ToWin32(c), out ushort h, out ushort l, out ushort s);
                double lum = tint < 0 ? l * (1 + tint) : l * (1 - tint) + (HlsMax - HlsMax * (1 - tint));
                return ColorTranslator.FromWin32(ColorHLSToRGB(h, (ushort)Math.Clamp(Math.Round(lum), 0, HlsMax), s));
            }
            catch (Exception) when (!OperatingSystem.IsWindows())
            {
                RgbToHls(c, out double h, out double l, out double s);
                l = tint < 0 ? l * (1 + tint) : l * (1 - tint) + tint;
                return HlsToRgb(h, Math.Clamp(l, 0, 1), s);
            }
        }

        [System.Runtime.InteropServices.DllImport("shlwapi.dll")]
        private static extern void ColorRGBToHLS(int rgb, out ushort hue, out ushort luminance, out ushort saturation);

        [System.Runtime.InteropServices.DllImport("shlwapi.dll")]
        private static extern int ColorHLSToRGB(ushort hue, ushort luminance, ushort saturation);

        private static void RgbToHls(Color c, out double h, out double l, out double s)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            if (max == min) { h = 0; s = 0; return; }
            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            h /= 6;
        }

        private static Color HlsToRgb(double h, double l, double s)
        {
            if (s == 0) { int v = (int)Math.Round(l * 255); return Color.FromArgb(v, v, v); }
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            double Hue(double t)
            {
                if (t < 0) t += 1; if (t > 1) t -= 1;
                if (t < 1.0 / 6) return p + (q - p) * 6 * t;
                if (t < 0.5) return q;
                if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
                return p;
            }
            return Color.FromArgb((int)Math.Round(Hue(h + 1.0 / 3) * 255), (int)Math.Round(Hue(h) * 255), (int)Math.Round(Hue(h - 1.0 / 3) * 255));
        }

        private static Color Hex(string rgb) =>
            Color.FromArgb(Convert.ToInt32(rgb[0..2], 16), Convert.ToInt32(rgb[2..4], 16), Convert.ToInt32(rgb[4..6], 16));

        // Excel's legacy indexed palette (64 = system foreground, 65 = system background)
        private static Color Indexed(int i)
        {
            string[] palette =
            {
                "000000","FFFFFF","FF0000","00FF00","0000FF","FFFF00","FF00FF","00FFFF","000000","FFFFFF","FF0000","00FF00","0000FF","FFFF00","FF00FF","00FFFF",
                "800000","008000","000080","808000","800080","008080","C0C0C0","808080","9999FF","993366","FFFFCC","CCFFFF","660066","FF8080","0066CC","CCCCFF",
                "000080","FF00FF","FFFF00","00FFFF","800080","800000","008080","0000FF","00CCFF","CCFFFF","CCFFCC","FFFF99","99CCFF","FF99CC","CC99FF","FFCC99",
                "3366FF","33CCCC","99CC00","FFCC00","FF9900","FF6600","666699","969696","003366","339966","003300","333300","993300","993366","333399","333333",
            };
            if (i == 64) return Color.Black;
            if (i == 65) return Color.White;
            return i >= 0 && i < palette.Length ? Hex(palette[i]) : Color.Black;
        }

        private static (int R1, int C1, int R2, int C2) ParseRange(string a1)
        {
            string[] parts = a1.Replace("$", "").Split(':');
            var (r1, c1) = ParseCell(parts[0]);
            var (r2, c2) = parts.Length > 1 ? ParseCell(parts[1]) : (r1, c1);
            return (r1, c1, r2, c2);
        }

        private static (int R, int C) ParseCell(string cell)
        {
            int i = 0, c = 0;
            while (i < cell.Length && char.IsLetter(cell[i])) c = c * 26 + (char.ToUpperInvariant(cell[i++]) - 'A' + 1);
            return (int.Parse(cell[i..], CultureInfo.InvariantCulture), c);
        }
    }
}
