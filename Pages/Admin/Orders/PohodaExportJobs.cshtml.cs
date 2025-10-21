using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services.Pohoda;

namespace SysJaky_N.Pages.Admin.Orders;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class PohodaExportJobsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPohodaExportService _pohodaExportService;
    private readonly TimeProvider _timeProvider;
    private readonly IStringLocalizer<PohodaExportJobsModel> _localizer;
    private readonly ILogger<PohodaExportJobsModel> _logger;

    public PohodaExportJobsModel(
        ApplicationDbContext dbContext,
        IPohodaExportService pohodaExportService,
        TimeProvider timeProvider,
        IStringLocalizer<PohodaExportJobsModel> localizer,
        ILogger<PohodaExportJobsModel> logger)
    {
        _dbContext = dbContext;
        _pohodaExportService = pohodaExportService;
        _timeProvider = timeProvider;
        _localizer = localizer;
        _logger = logger;
    }

    public IReadOnlyCollection<PohodaExportJobRow> Jobs { get; private set; } = Array.Empty<PohodaExportJobRow>();

    public IDictionary<PohodaExportJobStatus, int> StatusCounts { get; private set; } =
        new Dictionary<PohodaExportJobStatus, int>();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var jobs = await _dbContext.PohodaExportJobs
            .AsNoTracking()
            .Include(job => job.Order)
                .ThenInclude(order => order!.Customer)
            .OrderByDescending(job => job.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        StatusCounts = jobs
            .GroupBy(job => job.Status)
            .ToDictionary(group => group.Key, group => group.Count());

        Jobs = jobs
            .Select(job => new PohodaExportJobRow
            {
                Id = job.Id,
                OrderId = job.OrderId,
                CustomerEmail = job.Order?.Customer?.Email,
                Status = job.Status,
                AttemptCount = job.AttemptCount,
                CreatedAtUtc = job.CreatedAtUtc,
                LastAttemptAtUtc = job.LastAttemptAtUtc,
                NextAttemptAtUtc = job.NextAttemptAtUtc,
                SucceededAtUtc = job.SucceededAtUtc,
                FailedAtUtc = job.FailedAtUtc,
                LastError = job.LastError
            })
            .ToList();
    }

    public async Task<IActionResult> OnPostRetryAsync(int id, CancellationToken cancellationToken)
    {
        var job = await _dbContext.PohodaExportJobs
            .SingleOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job is null)
        {
            StatusMessage = _localizer["StatusJobNotFound", id];
            return RedirectToPage();
        }

        job.Status = PohodaExportJobStatus.Pending;
        job.NextAttemptAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        job.FailedAtUtc = null;
        job.LastError = null;
        job.LastAttemptAtUtc = null;
        job.DocumentNumber = null;
        job.DocumentId = null;
        job.Warnings = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        StatusMessage = _localizer["StatusJobRequeued", id];
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostForceAsync(int id, CancellationToken cancellationToken)
    {
        var job = await _dbContext.PohodaExportJobs
            .Include(j => j.Order)
                .ThenInclude(o => o!.Items)
                    .ThenInclude(i => i.Course)
            .SingleOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job is null)
        {
            StatusMessage = _localizer["StatusJobNotFound", id];
            return RedirectToPage();
        }

        try
        {
            await _pohodaExportService.ExportOrderAsync(job, cancellationToken);
            StatusMessage = _localizer["StatusJobExecuted", id];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual Pohoda export failed for job {JobId}.", id);
            StatusMessage = _localizer["StatusJobExecutionFailed", id];
        }

        return RedirectToPage();
    }

    public sealed class PohodaExportJobRow
    {
        public int Id { get; init; }
        public int OrderId { get; init; }
        public string? CustomerEmail { get; init; }
        public PohodaExportJobStatus Status { get; init; }
        public int AttemptCount { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? LastAttemptAtUtc { get; init; }
        public DateTime? NextAttemptAtUtc { get; init; }
        public DateTime? SucceededAtUtc { get; init; }
        public DateTime? FailedAtUtc { get; init; }
        public string? LastError { get; init; }
    }
}
