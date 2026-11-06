using System.Globalization;
using Accounting_System.Models;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace Accounting_System.Repository;

public partial class WorkbookImport
{
    private async Task ImportChartOfAccountAsync(ExcelWorksheet worksheet, CancellationToken token)
    {
        // Exports can include extra status columns. Never interpret a parent/status column as the source ID.
        static string Header(string value) => string.Concat(value.Where(char.IsLetterOrDigit));
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= worksheet.Dimension.Columns; column++)
        {
            var name = Header(worksheet.Cells[1, column].Text);
            if (name.Length > 0 && !columns.TryAdd(name, column))
                throw new InvalidOperationException($"Duplicate chart-of-account header '{name}'.");
        }
        if (!columns.ContainsKey("OriginalChartOfAccountId"))
        {
            if (columns.TryGetValue("OriginalChartOfAccount", out var sourceColumn) || columns.TryGetValue("AccountId", out sourceColumn))
                columns.Add("OriginalChartOfAccountId", sourceColumn);
        }
        foreach (var name in new[] { "IsMain", "AccountNumber", "AccountName", "AccountType", "NormalBalance", "Level",
                     "CreatedBy", "CreatedDate", "EditedBy", "EditedDate", "HasChildren", "ParentAccountId", "OriginalChartOfAccountId" })
            if (!columns.ContainsKey(name)) throw new InvalidOperationException($"Missing chart-of-account header '{name}'.");

        var rows = new Dictionary<int, (ChartOfAccount Account, int? ParentId, int Row)>();
        for (var row = 2; row <= worksheet.Dimension.Rows; row++)
        {
            ExcelRange Cell(string name) => worksheet.Cells[row, columns[name]];
            string Text(string name) => Cell(name).Text.Trim();
            if (worksheet.Cells[row, 1, row, worksheet.Dimension.Columns].All(x => string.IsNullOrWhiteSpace(x.Text))) continue;
            int Integer(string name, bool required = false)
            {
                var text = Text(name);
                if (text.Length == 0 && !required) return 0;
                if (int.TryParse(text, out var value) && (!required || value > 0)) return value;
                throw new InvalidOperationException($"Invalid '{name}' at chart-of-account row {row}.");
            }
            bool Boolean(string name)
            {
                if (bool.TryParse(Text(name), out var value)) return value;
                throw new InvalidOperationException($"Invalid '{name}' at chart-of-account row {row}.");
            }
            DateTime Date(string name)
            {
                var cell = Cell(name);
                if (string.IsNullOrWhiteSpace(cell.Text)) return default;
                if (cell.Value is DateTime value) return value;
                if (cell.Value is double serial) return DateTime.FromOADate(serial);
                if (DateTime.TryParse(cell.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
                    DateTime.TryParse(cell.Text, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed)) return parsed;
                throw new InvalidOperationException($"Invalid '{name}' at chart-of-account row {row}.");
            }
            var id = Integer("OriginalChartOfAccountId", true);
            var parent = Integer("ParentAccountId");
            if (parent < 0 || parent == id)
                throw new InvalidOperationException($"Invalid parent account at chart-of-account row {row}.");
            var account = new ChartOfAccount
            {
                OriginalChartOfAccountId = id,
                AccountNumber = Text("AccountNumber"), AccountName = Text("AccountName"),
                AccountType = Text("AccountType"), NormalBalance = Text("NormalBalance"),
                IsMain = Boolean("IsMain"), HasChildren = Boolean("HasChildren"), Level = Integer("Level"),
                CreatedBy = Text("CreatedBy"), CreatedDate = Date("CreatedDate"),
                EditedBy = Text("EditedBy"), EditedDate = Date("EditedDate"),
                Parent = columns.ContainsKey("Parent") ? Text("Parent") : string.Empty
            };
            if (account.AccountNumber.Length == 0 || account.AccountName.Length == 0)
                throw new InvalidOperationException($"Account number/name missing at chart-of-account row {row}.");
            if (!rows.TryAdd(id, (account, parent == 0 ? null : parent, row)))
                throw new InvalidOperationException($"Duplicate original account ID {id} at row {row}. No rows were imported.");
        }

        var existing = await _dbContext.ChartOfAccounts.ToListAsync(token);
        var used = new HashSet<ChartOfAccount>();
        var targets = new Dictionary<int, ChartOfAccount>();
        foreach (var (id, row) in rows)
        {
            var sameId = existing.Where(x => x.OriginalChartOfAccountId == id && !used.Contains(x)).ToList();
            var sameNumber = existing.Where(x => x.AccountNumber == row.Account.AccountNumber && !used.Contains(x) &&
                (x.OriginalChartOfAccountId == id || !rows.TryGetValue(x.OriginalChartOfAccountId ?? 0, out var owner) ||
                 owner.Account.AccountNumber != x.AccountNumber)).ToList();
            var exact = sameId.Where(x => x.AccountNumber == row.Account.AccountNumber).ToList();
            if (exact.Count > 1 || (exact.Count == 0 && sameNumber.Count > 1))
                throw new InvalidOperationException($"Multiple existing accounts match row {row.Row}. Resolve duplicate account identities first.");
            var target = exact.SingleOrDefault() ?? sameNumber.SingleOrDefault();
            // Recover wrong source IDs from the earlier positional importer without stealing another uploaded account.
            if (target == null)
            {
                var available = sameId.Where(x => !rows.Values.Any(other => other.Account.OriginalChartOfAccountId != id &&
                    other.Account.AccountNumber == x.AccountNumber)).ToList();
                if (available.Count > 1) throw new InvalidOperationException($"Multiple existing accounts have original ID {id}.");
                target = available.SingleOrDefault();
            }
            if (target == null)
            {
                target = row.Account;
                _dbContext.ChartOfAccounts.Add(target);
            }
            used.Add(target);
            targets.Add(id, target);
        }
        // All local IDs must exist before parents are mapped; child rows may precede their parents.
        await _dbContext.SaveChangesAsync(token);
        foreach (var (id, row) in rows)
        {
            ChartOfAccount? parent = null;
            if (row.ParentId is int parentId)
            {
                if (!targets.TryGetValue(parentId, out parent))
                {
                    var candidates = existing.Where(x => x.OriginalChartOfAccountId == parentId && !used.Contains(x)).ToList();
                    if (candidates.Count == 1) parent = candidates[0];
                }
                if (parent == null) throw new InvalidOperationException($"Parent original ID {parentId} missing at chart-of-account row {row.Row}.");
            }
            row.Account.ParentAccountId = parent?.AccountId;
            var visited = new HashSet<int> { id };
            var next = row.ParentId;
            while (next is int ancestor && rows.TryGetValue(ancestor, out var ancestorRow))
            {
                if (!visited.Add(ancestor)) throw new InvalidOperationException($"Parent cycle at chart-of-account row {row.Row}.");
                next = ancestorRow.ParentId;
            }
            var target = targets[id];
            if (!ReferenceEquals(target, row.Account))
                await UpdateMasterAsync(target, row.Account, "ChartOfAccount", token,
                    columns.ContainsKey("Parent") ? [] : [nameof(ChartOfAccount.Parent)]);
        }
        await _dbContext.SaveChangesAsync(token);
    }
}
