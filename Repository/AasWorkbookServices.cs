using Accounting_System.Data;
using Accounting_System.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Accounting_System.Repository;

// Reuse the workbook workflow and repositories with AAS's connection and actual EF model.
// Identity/user services remain in the request scope; accounting repositories use the target context.
internal sealed class AasWorkbookServices(IServiceProvider request, ApplicationDbContext target) : IServiceProvider
{
    private readonly Dictionary<Type, object> _repositories = new();

    public object? GetService(Type type)
    {
        if (type == typeof(IServiceProvider)) return this;
        if (type == typeof(ApplicationDbContext)) return target;
        if (type.Namespace == typeof(WorkbookImport).Namespace && type.Name.EndsWith("Repo", StringComparison.Ordinal))
        {
            if (!_repositories.TryGetValue(type, out var repository))
                _repositories[type] = repository = ActivatorUtilities.CreateInstance(this, type);
            return repository;
        }
        return request.GetService(type);
    }

    public static ApplicationDbContext CreateTarget(AasDbContext aas)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        foreach (var extension in aas.GetService<IDbContextOptions>().Extensions)
            ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(extension);
        options.UseModel(aas.Model);
        return new AasWorkbookContext(options.Options);
    }

    // Proposals are committed alongside AAS records, then published to the review database.
    // Pending proposals can be republished after a failed review-database save without reimporting records.
    public static async Task PublishAsync(ApplicationDbContext target, ApplicationDbContext review, CancellationToken token)
    {
        var pending = await target.ImportExportLogs.AsNoTracking()
            .Where(x => x.DatabaseName == "AAS" && x.Action == string.Empty && !x.Executed).ToListAsync(token);
        var ids = pending.Select(x => x.Id).ToList();
        var published = (await review.ImportExportLogs.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(token)).ToHashSet();
        foreach (var log in pending)
            if (!published.Contains(log.Id)) review.ImportExportLogs.Add(log);
        await review.SaveChangesAsync(token);
    }

    private sealed class AasWorkbookContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<ImportExportLog>().Where(x => x.State == EntityState.Added))
                entry.Entity.DatabaseName = "AAS";
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
