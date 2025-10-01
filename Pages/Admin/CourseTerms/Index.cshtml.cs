using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Pages.Admin.CourseTerms;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public IndexModel(ApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public IList<CourseTerm> Terms { get; private set; } = new List<CourseTerm>();

    public List<SelectListItem> CourseOptions { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? CourseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool OnlyActive { get; set; }

    [BindProperty]
    public IFormFile? ImportFile { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostExportEnrollmentsAsync(int id)
    {
        var term = await _context.CourseTerms
            .AsNoTracking()
            .Include(t => t.Course)
            .Include(t => t.Enrollments)
                .ThenInclude(e => e.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (term == null)
        {
            return NotFound();
        }

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Enrollments");

        var headers = new[]
        {
            "EnrollmentId",
            "Status",
            "UserId",
            "UserEmail",
            "UserName",
            "UserPhoneNumber",
            "CourseTermId",
            "CourseTitle",
            "Start (UTC)",
            "End (UTC)"
        };

        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cells[1, column + 1].Value = headers[column];
            worksheet.Cells[1, column + 1].Style.Font.Bold = true;
        }

        var enrollments = term.Enrollments
            .OrderBy(e => e.Id)
            .ToList();

        for (var index = 0; index < enrollments.Count; index++)
        {
            var row = index + 2;
            var enrollment = enrollments[index];

            worksheet.Cells[row, 1].Value = enrollment.Id;
            worksheet.Cells[row, 2].Value = enrollment.Status.ToString();
            worksheet.Cells[row, 3].Value = enrollment.UserId;
            worksheet.Cells[row, 4].Value = enrollment.User?.Email;
            worksheet.Cells[row, 5].Value = enrollment.User?.UserName;
            worksheet.Cells[row, 6].Value = enrollment.User?.PhoneNumber;
            worksheet.Cells[row, 7].Value = term.Id;
            worksheet.Cells[row, 8].Value = term.Course?.Title ?? $"Course {term.CourseId}";

            var startUtc = EnsureUtc(term.StartUtc);
            var endUtc = EnsureUtc(term.EndUtc);

            worksheet.Cells[row, 9].Value = startUtc;
            worksheet.Cells[row, 9].Style.Numberformat.Format = "yyyy-mm-dd hh:mm";

            worksheet.Cells[row, 10].Value = endUtc;
            worksheet.Cells[row, 10].Style.Numberformat.Format = "yyyy-mm-dd hh:mm";
        }

        if (worksheet.Dimension != null)
        {
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        var courseTitle = term.Course?.Title ?? $"Kurz_{term.CourseId}";
        var safeCourseTitle = SanitizeForFileName(courseTitle);
        var fileName = $"{safeCourseTitle}_term_{term.Id}_enrollments.xlsx";
        var content = package.GetAsByteArray();

        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        if (ImportFile == null || ImportFile.Length == 0)
        {
            ModelState.AddModelError(nameof(ImportFile), "Vyberte XLSX soubor pro import.");
            ErrorMessage = "Import se nezdařil. Zkontrolujte zvýrazněné problémy.";
            await LoadPageDataAsync();
            return Page();
        }

        if (!string.Equals(Path.GetExtension(ImportFile.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(ImportFile), "Podporovány jsou pouze soubory XLSX.");
            ErrorMessage = "Import se nezdařil. Zkontrolujte zvýrazněné problémy.";
            await LoadPageDataAsync();
            return Page();
        }

        var parsedRows = new List<CourseTermImportRow>();
        var courseIds = new HashSet<int>();
        var instructorIds = new HashSet<int>();

        using (var stream = new MemoryStream())
        {
            await ImportFile.CopyToAsync(stream);
            stream.Position = 0;

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null || worksheet.Dimension == null)
            {
                ModelState.AddModelError(nameof(ImportFile), "Nahraný soubor neobsahuje žádná data.");
                ErrorMessage = "Import se nezdařil. Zkontrolujte zvýrazněné problémy.";
                await LoadPageDataAsync();
                return Page();
            }

            var headerMap = ReadHeaderMap(worksheet);
            var requiredHeaders = new[] { "CourseId", "StartUtc", "EndUtc", "Capacity" };
            foreach (var header in requiredHeaders)
            {
                if (!headerMap.ContainsKey(header))
                {
                    ModelState.AddModelError(nameof(ImportFile), $"Chybí povinný sloupec '{header}'.");
                }
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Import se nezdařil. Zkontrolujte zvýrazněné problémy.";
                await LoadPageDataAsync();
                return Page();
            }

            var lastRow = worksheet.Dimension.End.Row;
            for (var row = 2; row <= lastRow; row++)
            {
                if (IsRowEmpty(worksheet, row))
                {
                    continue;
                }

                var rowErrors = new List<string>();

                int? id = null;
                if (TryGetColumn(headerMap, "Id", out var idColumn))
                {
                    if (!TryGetNullableInt(worksheet.Cells[row, idColumn], out id))
                    {
                        rowErrors.Add("Neplatná hodnota ve sloupci Id.");
                    }
                }

                if (!TryGetInt(worksheet.Cells[row, headerMap["CourseId"]], out var courseId))
                {
                    rowErrors.Add("CourseId je povinný a musí být číslo.");
                }
                else
                {
                    courseIds.Add(courseId);
                }

                if (!TryGetDateTime(worksheet.Cells[row, headerMap["StartUtc"]], out var startUtc))
                {
                    rowErrors.Add("StartUtc je povinný a musí být platné datum a čas.");
                }

                if (!TryGetDateTime(worksheet.Cells[row, headerMap["EndUtc"]], out var endUtc))
                {
                    rowErrors.Add("EndUtc je povinný a musí být platné datum a čas.");
                }

                if (!TryGetInt(worksheet.Cells[row, headerMap["Capacity"]], out var capacity))
                {
                    rowErrors.Add("Capacity je povinná a musí být číslo.");
                }
                else if (capacity < 1)
                {
                    rowErrors.Add("Kapacita musí být alespoň 1.");
                }

                bool? isActive = null;
                if (TryGetColumn(headerMap, "IsActive", out var isActiveColumn))
                {
                    if (!TryGetNullableBool(worksheet.Cells[row, isActiveColumn], out isActive))
                    {
                        rowErrors.Add("IsActive musí být logická hodnota.");
                    }
                }

                int? instructorId = null;
                if (TryGetColumn(headerMap, "InstructorId", out var instructorColumn))
                {
                    if (!TryGetNullableInt(worksheet.Cells[row, instructorColumn], out instructorId))
                    {
                        rowErrors.Add("InstructorId musí být číslo.");
                    }
                    else if (instructorId.HasValue)
                    {
                        instructorIds.Add(instructorId.Value);
                    }
                }

                if (!rowErrors.Any() && EnsureUtc(startUtc) >= EnsureUtc(endUtc))
                {
                    rowErrors.Add("StartUtc musí být dříve než EndUtc.");
                }

                if (rowErrors.Count > 0)
                {
                    ModelState.AddModelError(nameof(ImportFile), $"Row {row}: {string.Join(" ", rowErrors)}");
                    continue;
                }

                parsedRows.Add(new CourseTermImportRow
                {
                    RowNumber = row,
                    Id = id,
                    CourseId = courseId,
                    StartUtc = EnsureUtc(startUtc),
                    EndUtc = EnsureUtc(endUtc),
                    Capacity = capacity,
                    IsActive = isActive,
                    InstructorId = instructorId
                });
            }
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Import se nezdařil. Zkontrolujte zvýrazněné problémy.";
            await LoadPageDataAsync();
            return Page();
        }

        if (parsedRows.Count == 0)
        {
            StatusMessage = "V nahraném souboru nebyly nalezeny žádné termíny.";
            return RedirectToPage(new { CourseId, OnlyActive });
        }

        var existingCourseIds = await _context.Courses
            .Where(c => courseIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        foreach (var missingCourseId in courseIds.Except(existingCourseIds))
        {
            ModelState.AddModelError(nameof(ImportFile), $"Kurz s ID {missingCourseId} neexistuje.");
        }

        if (instructorIds.Count > 0)
        {
            var existingInstructorIds = await _context.Instructors
                .Where(i => instructorIds.Contains(i.Id))
                .Select(i => i.Id)
                .ToListAsync();

            foreach (var missingInstructorId in instructorIds.Except(existingInstructorIds))
            {
                ModelState.AddModelError(nameof(ImportFile), $"Lektor s ID {missingInstructorId} neexistuje.");
            }
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Import se nezdařil. Zkontrolujte zvýrazněné problémy.";
            await LoadPageDataAsync();
            return Page();
        }

        var rowsWithIds = parsedRows.Where(r => r.Id.HasValue).ToList();
        var termsToUpdate = new Dictionary<int, CourseTerm>();

        if (rowsWithIds.Count > 0)
        {
            var termIds = rowsWithIds.Select(r => r.Id!.Value).ToHashSet();
            termsToUpdate = await _context.CourseTerms
                .Where(t => termIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id);

            foreach (var missingTermId in termIds.Except(termsToUpdate.Keys))
            {
                ModelState.AddModelError(nameof(ImportFile), $"Termín s ID {missingTermId} neexistuje.");
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Import se nezdařil. Zkontrolujte zvýrazněné problémy.";
                await LoadPageDataAsync();
                return Page();
            }
        }

        var created = 0;
        var updated = 0;
        var affectedCourseIds = new HashSet<int>();

        foreach (var row in parsedRows)
        {
            CourseTerm term;
            int? previousCourseId = null;
            if (row.Id.HasValue)
            {
                term = termsToUpdate[row.Id.Value];
                if (term.SeatsTaken > row.Capacity)
                {
                    ModelState.AddModelError(nameof(ImportFile), $"Řádek {row.RowNumber}: Kapacita {row.Capacity} je menší než aktuálně obsazená místa ({term.SeatsTaken}).");
                    continue;
                }

                previousCourseId = term.CourseId;
                updated++;
            }
            else
            {
                term = new CourseTerm
                {
                    SeatsTaken = 0
                };
                _context.CourseTerms.Add(term);
                created++;
            }

            term.CourseId = row.CourseId;
            term.StartUtc = row.StartUtc;
            term.EndUtc = row.EndUtc;
            term.Capacity = row.Capacity;

            if (row.IsActive.HasValue)
            {
                term.IsActive = row.IsActive.Value;
            }

            term.InstructorId = row.InstructorId;

            affectedCourseIds.Add(term.CourseId);
            if (previousCourseId.HasValue && previousCourseId.Value != term.CourseId)
            {
                affectedCourseIds.Add(previousCourseId.Value);
            }
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Import se nezdařil. Zkontrolujte zvýrazněné problémy.";
            await LoadPageDataAsync();
            return Page();
        }

        await _context.SaveChangesAsync();

        _cacheService.InvalidateCourseList();
        foreach (var courseId in affectedCourseIds)
        {
            _cacheService.InvalidateCourseDetail(courseId);
        }

        StatusMessage = $"Importováno {created} nových termínů a aktualizováno {updated} stávajících termínů.";
        return RedirectToPage(new { CourseId, OnlyActive });
    }

    private async Task LoadPageDataAsync()
    {
        var courseQuery = _context.Courses
            .AsNoTracking()
            .OrderBy(c => c.Title)
            .Select(c => new SelectListItem(c.Title, c.Id.ToString(), CourseId == c.Id));

        CourseOptions = await courseQuery.ToListAsync();

        var termQuery = _context.CourseTerms
            .AsNoTracking()
            .Include(t => t.Course)
            .Include(t => t.Instructor)
            .AsQueryable();

        if (CourseId.HasValue)
        {
            termQuery = termQuery.Where(t => t.CourseId == CourseId);
        }

        if (OnlyActive)
        {
            termQuery = termQuery.Where(t => t.IsActive);
        }

        Terms = await termQuery
            .OrderByDescending(t => t.StartUtc)
            .ToListAsync();
    }

    private static Dictionary<string, int> ReadHeaderMap(ExcelWorksheet worksheet)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var endColumn = worksheet.Dimension?.End.Column ?? 0;

        for (var column = 1; column <= endColumn; column++)
        {
            var header = worksheet.Cells[1, column].GetValue<string>();
            if (!string.IsNullOrWhiteSpace(header))
            {
                headers[header.Trim()] = column;
            }
        }

        return headers;
    }

    private static bool TryGetColumn(Dictionary<string, int> headerMap, string key, out int column)
    {
        return headerMap.TryGetValue(key, out column);
    }

    private static bool IsRowEmpty(ExcelWorksheet worksheet, int row)
    {
        var endColumn = worksheet.Dimension?.End.Column ?? 0;
        for (var column = 1; column <= endColumn; column++)
        {
            var value = worksheet.Cells[row, column].Value;
            if (value is string stringValue)
            {
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    return false;
                }
            }
            else if (value != null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetInt(ExcelRange cell, out int value)
    {
        var success = TryGetNullableInt(cell, out var nullable);
        if (success && nullable.HasValue)
        {
            value = nullable.Value;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetNullableInt(ExcelRange cell, out int? value)
    {
        var raw = cell.Value;

        if (raw == null)
        {
            value = null;
            return true;
        }

        if (raw is string stringValue && string.IsNullOrWhiteSpace(stringValue))
        {
            value = null;
            return true;
        }

        if (raw is int intValue)
        {
            value = intValue;
            return true;
        }

        if (raw is long longValue)
        {
            value = Convert.ToInt32(longValue);
            return true;
        }

        if (raw is double doubleValue)
        {
            if (Math.Abs(doubleValue % 1d) > double.Epsilon)
            {
                value = null;
                return false;
            }

            value = Convert.ToInt32(Math.Round(doubleValue, MidpointRounding.AwayFromZero));
            return true;
        }

        if (raw is decimal decimalValue)
        {
            if (decimalValue % 1m != 0m)
            {
                value = null;
                return false;
            }

            value = (int)decimalValue;
            return true;
        }

        if (raw is string stringNumber)
        {
            if (int.TryParse(stringNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
                int.TryParse(stringNumber, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed))
            {
                value = parsed;
                return true;
            }

            value = null;
            return false;
        }

        value = null;
        return false;
    }

    private static bool TryGetDateTime(ExcelRange cell, out DateTime value)
    {
        var raw = cell.Value;

        if (raw == null)
        {
            value = default;
            return false;
        }

        if (raw is DateTime dateTime)
        {
            value = dateTime;
            return true;
        }

        if (raw is double doubleValue)
        {
            value = DateTime.FromOADate(doubleValue);
            return true;
        }

        if (raw is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                value = default;
                return false;
            }

            if (DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
                DateTime.TryParse(stringValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
            {
                value = parsed;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetNullableBool(ExcelRange cell, out bool? value)
    {
        var raw = cell.Value;

        if (raw == null)
        {
            value = null;
            return true;
        }

        if (raw is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                value = null;
                return true;
            }

            var normalized = stringValue.Trim();

            if (bool.TryParse(normalized, out var parsedBool))
            {
                value = parsedBool;
                return true;
            }

            if (string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "ano", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "ne", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            value = null;
            return false;
        }

        if (raw is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        if (raw is double doubleValue)
        {
            if (Math.Abs(doubleValue) < double.Epsilon)
            {
                value = false;
                return true;
            }

            if (Math.Abs(doubleValue - 1d) < double.Epsilon)
            {
                value = true;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string SanitizeForFileName(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(input.Length);

        foreach (var character in input)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        var sanitized = builder.ToString();
        return string.IsNullOrWhiteSpace(sanitized) ? "course" : sanitized;
    }

    private sealed class CourseTermImportRow
    {
        public int RowNumber { get; init; }
        public int? Id { get; init; }
        public int CourseId { get; init; }
        public DateTime StartUtc { get; init; }
        public DateTime EndUtc { get; init; }
        public int Capacity { get; init; }
        public bool? IsActive { get; init; }
        public int? InstructorId { get; init; }
    }
}
