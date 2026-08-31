using System.Collections;
using System.Reflection;
using ClosedXML.Excel;

namespace ReportingService.Infrastructure.Services;

/// <summary>
/// Generic reflection-based Excel renderer for any of the 9 report DTOs — no per-report template
/// needed. Scalar (non-collection) top-level properties go on a "Summary" sheet as Property|Value
/// rows; each List&lt;T&gt; property gets its own sheet named after the property, with T's own
/// properties as columns. PDF isn't implemented yet — see ReportSchedulerBackgroundService.
/// </summary>
internal static class ReportRenderer
{
    public static byte[] ToExcel(object report)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Summary");
        var row = 1;

        var type = report.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0);
        foreach (var prop in properties)
        {
            var value = prop.GetValue(report);

            if (value is IEnumerable enumerable && prop.PropertyType != typeof(string))
            {
                WriteListSheet(workbook, prop.Name, enumerable);
                continue;
            }

            summary.Cell(row, 1).Value = prop.Name;
            summary.Cell(row, 2).Value = value?.ToString() ?? string.Empty;
            row++;
        }

        summary.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteListSheet(XLWorkbook workbook, string sheetName, IEnumerable items)
    {
        var itemList = items.Cast<object?>().Where(i => i != null).Select(i => i!).ToList();
        var sheet = workbook.Worksheets.Add(SafeSheetName(sheetName));
        if (itemList.Count == 0)
        {
            sheet.Cell(1, 1).Value = "(no rows)";
            return;
        }

        var itemType = itemList[0].GetType();

        // Scalars (string, decimal, DateTime, enum, Guid, etc.) have no properties worth
        // reflecting — e.g. reflecting string.GetProperties() picks up its indexer "Chars", whose
        // GetValue(obj) throws "Parameter count mismatch" since it needs an index argument.
        if (IsSimpleType(itemType))
        {
            sheet.Cell(1, 1).Value = "Value";
            for (var r = 0; r < itemList.Count; r++)
                sheet.Cell(r + 2, 1).Value = itemList[r].ToString() ?? string.Empty;
            sheet.Columns().AdjustToContents();
            return;
        }

        // Exclude indexer properties (e.g. Dictionary<K,V>'s this[K]) for the same reason.
        var props = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .ToArray();

        for (var col = 0; col < props.Length; col++)
            sheet.Cell(1, col + 1).Value = props[col].Name;

        for (var r = 0; r < itemList.Count; r++)
        {
            for (var col = 0; col < props.Length; col++)
            {
                var value = props[col].GetValue(itemList[r]);
                sheet.Cell(r + 2, col + 1).Value = value?.ToString() ?? string.Empty;
            }
        }

        sheet.Columns().AdjustToContents();
    }

    private static bool IsSimpleType(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
        type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid) || type == typeof(TimeSpan);

    // Excel sheet names cap at 31 chars and disallow a handful of characters.
    private static string SafeSheetName(string name)
    {
        var cleaned = new string(name.Where(c => !"[]:*?/\\".Contains(c)).ToArray());
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
}
