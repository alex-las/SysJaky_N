using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QRCoder;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class CertificateOptions
{
    public string PublicBaseUrl { get; set; } = "https://localhost";
    public string OutputDirectory { get; set; } = "certificates";
    public string Title { get; set; } = "Certifikát o absolvování";
}

public class CertificateService
{
    private readonly ApplicationDbContext _context;
    private readonly IConverter _converter;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CertificateService> _logger;
    private readonly CertificateOptions _options;

    public CertificateService(
        ApplicationDbContext context,
        IConverter converter,
        IWebHostEnvironment environment,
        IOptions<CertificateOptions> options,
        ILogger<CertificateService> logger)
    {
        _context = context;
        _converter = converter;
        _environment = environment;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<int> IssueCertificatesForCompletedEnrollmentsAsync(CancellationToken cancellationToken = default)
    {
        var enrollments = await _context.Enrollments
            .Include(e => e.Certificate)
            .Include(e => e.User)
            .Include(e => e.CourseTerm)
                .ThenInclude(t => t.Course)
            .Include(e => e.Attendance)
            .Where(e => e.Attendance != null)
            .Where(e => e.Certificate == null)
            .ToListAsync(cancellationToken);

        var issued = 0;
        foreach (var enrollment in enrollments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await IssueCertificateInternalAsync(enrollment, cancellationToken);
                issued++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error issuing certificate for enrollment {EnrollmentId}.", enrollment.Id);
            }
        }

        return issued;
    }

    public async Task<Certificate?> IssueCertificateForEnrollmentAsync(int enrollmentId, CancellationToken cancellationToken = default)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Certificate)
            .Include(e => e.User)
            .Include(e => e.CourseTerm)
                .ThenInclude(t => t.Course)
            .Include(e => e.Attendance)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);

        if (enrollment == null)
        {
            return null;
        }

        if (enrollment.Certificate != null)
        {
            return enrollment.Certificate;
        }

        if (enrollment.Attendance?.CheckedInAtUtc == null)
        {
            throw new InvalidOperationException("Enrollment has not been marked as completed.");
        }

        return await IssueCertificateInternalAsync(enrollment, cancellationToken);
    }

    private async Task<Certificate> IssueCertificateInternalAsync(Enrollment enrollment, CancellationToken cancellationToken)
    {
        if (enrollment.Certificate != null)
        {
            return enrollment.Certificate;
        }

        var number = await GenerateUniqueNumberAsync(cancellationToken);
        var hash = GenerateHashString();
        var verifyUrl = BuildVerifyUrl(number, hash);

        var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(verifyUrl, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(20);
        var qrBase64 = Convert.ToBase64String(qrBytes);

        var html = BuildHtml(enrollment, number, verifyUrl, qrBase64);

        var document = new HtmlToPdfDocument
        {
            GlobalSettings = new GlobalSettings
            {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Landscape,
                PaperSize = PaperKind.A4,
                Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 }
            },
            Objects =
            {
                new ObjectSettings
                {
                    HtmlContent = html,
                    WebSettings = { DefaultEncoding = "utf-8" }
                }
            }
        };

        var pdfBytes = _converter.Convert(document);

        var (outputDirectory, relativeDirectory) = EnsureOutputDirectory();
        var fileName = $"certificate_{number}.pdf";
        var filePath = Path.Combine(outputDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, pdfBytes, cancellationToken);

        var sanitizedDirectory = relativeDirectory.Replace('\\', '/').Trim('/');
        var relativePath = string.IsNullOrEmpty(sanitizedDirectory)
            ? $"/{fileName}"
            : $"/{sanitizedDirectory}/{fileName}";

        var certificate = new Certificate
        {
            Number = number,
            Hash = hash,
            VerifyUrl = verifyUrl,
            PdfPath = relativePath,
            IssuedToEnrollmentId = enrollment.Id
        };

        enrollment.Certificate = certificate;
        _context.Certificates.Add(certificate);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Certificate {Number} issued for enrollment {EnrollmentId}.", number, enrollment.Id);
        return certificate;
    }

    private async Task<string> GenerateUniqueNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var randomSuffix = RandomNumberGenerator.GetInt32(1000, 9999);
            var candidate = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{randomSuffix}";
            var exists = await _context.Certificates.AnyAsync(c => c.Number == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var buffer = new byte[8];
            RandomNumberGenerator.Fill(buffer);
            var candidate = Convert.ToHexString(buffer);

            var exists = await _context.Certificates.AnyAsync(c => c.Number == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }
    }

    private static string GenerateHashString()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer);
    }

    private string BuildVerifyUrl(string number, string hash)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? "https://localhost"
            : _options.PublicBaseUrl.Trim();

        if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/", UriKind.Absolute, out var baseUri))
        {
            baseUri = new Uri("https://localhost");
        }

        var verifyUri = new Uri(baseUri, $"verify/{Uri.EscapeDataString(number)}/{Uri.EscapeDataString(hash)}");
        return verifyUri.ToString();
    }

    private (string OutputDirectory, string RelativeDirectory) EnsureOutputDirectory()
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        var directorySetting = string.IsNullOrWhiteSpace(_options.OutputDirectory)
            ? "certificates"
            : _options.OutputDirectory;

        var segments = directorySetting
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            segments = new[] { "certificates" };
        }

        var pathSegments = new string[segments.Length + 1];
        pathSegments[0] = webRoot;
        Array.Copy(segments, 0, pathSegments, 1, segments.Length);
        var absolutePath = Path.Combine(pathSegments);
        Directory.CreateDirectory(absolutePath);

        var relative = string.Join('/', segments);
        return (absolutePath, relative);
    }

    private string BuildHtml(Enrollment enrollment, string number, string verifyUrl, string qrBase64)
    {
        var culture = CultureInfo.GetCultureInfo("cs-CZ");
        var participant = enrollment.User?.Email ?? enrollment.UserId;
        var courseTitle = enrollment.CourseTerm?.Course?.Title ?? $"Kurz {enrollment.CourseTerm?.CourseId}";
        var start = enrollment.CourseTerm?.StartUtc.ToLocalTime();
        var end = enrollment.CourseTerm?.EndUtc.ToLocalTime();
        var completionUtc = enrollment.Attendance?.CheckedInAtUtc;
        var completion = completionUtc?.ToLocalTime();

        var termInfo = start.HasValue && end.HasValue && end.Value != start.Value
            ? $"{start.Value.ToString("d. MMMM yyyy H:mm", culture)} – {end.Value.ToString("d. MMMM yyyy H:mm", culture)}"
            : start?.ToString("d. MMMM yyyy H:mm", culture) ?? "-";
        var completionInfo = completion?.ToString("d. MMMM yyyy H:mm", culture) ?? "-";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='cs'>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='utf-8' />");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; background-color: #f8f9fa; margin: 0; padding: 0; }");
        sb.AppendLine(".certificate { margin: 40px; padding: 40px; border: 12px double #0d6efd; background-color: #ffffff; text-align: center; }");
        sb.AppendLine("h1 { font-size: 32px; text-transform: uppercase; margin-bottom: 10px; }");
        sb.AppendLine(".detail { font-size: 18px; margin: 12px 0; }");
        sb.AppendLine(".qr { margin-top: 30px; }");
        sb.AppendLine(".qr img { width: 160px; height: 160px; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class='certificate'>");
        sb.AppendLine($"    <h1>{WebUtility.HtmlEncode(_options.Title)}</h1>");
        sb.AppendLine($"    <p class='detail'>Číslo certifikátu: <strong>{WebUtility.HtmlEncode(number)}</strong></p>");
        sb.AppendLine($"    <p class='detail'>Absolvent: <strong>{WebUtility.HtmlEncode(participant)}</strong></p>");
        sb.AppendLine($"    <p class='detail'>Kurz: <strong>{WebUtility.HtmlEncode(courseTitle)}</strong></p>");
        sb.AppendLine($"    <p class='detail'>Termín kurzu: <strong>{WebUtility.HtmlEncode(termInfo)}</strong></p>");
        sb.AppendLine($"    <p class='detail'>Dokončeno dne: <strong>{WebUtility.HtmlEncode(completionInfo)}</strong></p>");
        sb.AppendLine("    <div class='qr'>");
        sb.AppendLine($"      <img src='data:image/png;base64,{qrBase64}' alt='QR kód pro ověření' />");
        sb.AppendLine($"      <p>{WebUtility.HtmlEncode(verifyUrl)}</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }
}
