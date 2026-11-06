using System.Globalization;
using OfficeOpenXml;

namespace Accounting_System.Utility;

internal sealed class ImportWorksheet
{
    private readonly ExcelWorksheet _worksheet;
    private readonly Dictionary<string, int> _columns = new(StringComparer.OrdinalIgnoreCase);

    public ImportWorksheet(ExcelWorksheet worksheet)
    {
        _worksheet = worksheet;
        if (worksheet.Dimension == null) throw new InvalidOperationException($"Worksheet '{worksheet.Name}' is empty.");
        for (var column = 1; column <= worksheet.Dimension.Columns; column++)
        {
            var header = Normalize(worksheet.Cells[1, column].Text);
            if (header.Length > 0 && !_columns.TryAdd(header, column))
                throw new InvalidOperationException($"Worksheet '{worksheet.Name}' has duplicate header '{header}'.");
        }
    }

    private static string Normalize(string text) => string.Concat(text.Where(char.IsLetterOrDigit));

    private ExcelRange Cell(int row, string field, params string[] aliases)
    {
        foreach (var name in new[] { field }.Concat(aliases))
            if (_columns.TryGetValue(Normalize(name), out var column)) return _worksheet.Cells[row, column];
        throw new InvalidOperationException($"Worksheet '{_worksheet.Name}' is missing header '{field}'.");
    }

    public bool HasColumn(string field) => _columns.ContainsKey(Normalize(field));

    public bool IsEmpty(int row) => _worksheet.Cells[row, 1, row, _worksheet.Dimension.Columns]
        .All(cell => string.IsNullOrWhiteSpace(cell.Text));

    public string Text(int row, string field, params string[] aliases) => StringHelper.NormalizeString(Cell(row, field, aliases).Text);

    public T Value<T>(int row, string field, params string[] aliases)
    {
        try { return Cell(row, field, aliases).GetValue<T>(); }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidOperationException($"Worksheet '{_worksheet.Name}', row {row}: invalid '{field}'.", ex);
        }
    }

    public T RequiredValue<T>(int row, string field, params string[] aliases)
    {
        var value = Cell(row, field, aliases).Value;
        if (value == null || value is string text && string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"Worksheet '{_worksheet.Name}', row {row}: '{field}' is required.");
        return Value<T>(row, field, aliases);
    }

    // Some existing voucher-detail exports omit these optional fields entirely.
    public T OptionalValue<T>(int row, string field) => _columns.ContainsKey(Normalize(field))
        ? Value<T>(row, field) : default!;

    public DateTime Date(int row, string field, bool required = false, params string[] aliases)
    {
        var cell = Cell(row, field, aliases);
        if (string.IsNullOrWhiteSpace(cell.Text))
        {
            if (!required) return default;
            throw new InvalidOperationException($"Worksheet '{_worksheet.Name}', row {row}: '{field}' is required.");
        }
        if (cell.Value is DateTime date) return date;
        if (cell.Value is double serial)
        {
            try { return DateTime.FromOADate(serial); }
            catch (ArgumentException) { }
        }
        if (DateTime.TryParse(cell.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
            DateTime.TryParse(cell.Text, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed)) return parsed;
        throw new InvalidOperationException($"Worksheet '{_worksheet.Name}', row {row}: invalid date in '{field}'.");
    }
}
