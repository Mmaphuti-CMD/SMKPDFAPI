using Microsoft.AspNetCore.Mvc;
using SMKPDFAPI.Models;
using SMKPDFAPI.Parsing;
using SMKPDFAPI.Pdf;

namespace SMKPDFAPI.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly IPdfTextExtractor _extractor;
    private readonly IStatementNormalizer _normalizer;
    private readonly ITransactionParser _parser;
    private readonly IIssuerExtractor _issuerExtractor;
    private readonly IDuplicateDetector _duplicateDetector;
    private readonly IAccountInfoExtractor _accountInfoExtractor;
    private readonly IStatementMetadataExtractor _metadataExtractor;

    public TransactionsController(
        IPdfTextExtractor extractor, 
        IStatementNormalizer normalizer, 
        ITransactionParser parser,
        IIssuerExtractor issuerExtractor,
        IDuplicateDetector duplicateDetector,
        IAccountInfoExtractor accountInfoExtractor,
        IStatementMetadataExtractor metadataExtractor)
    {
        _extractor = extractor;
        _normalizer = normalizer;
        _parser = parser;
        _issuerExtractor = issuerExtractor;
        _duplicateDetector = duplicateDetector;
        _accountInfoExtractor = accountInfoExtractor;
        _metadataExtractor = metadataExtractor;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { message = "Use POST to upload a PDF file", endpoint = "/api/transactions", method = "POST", contentType = "multipart/form-data" });
    }

    /// <summary>
    /// Upload a PDF bank statement to extract transactions
    /// </summary>
    /// <param name="file">PDF file to upload</param>
    /// <param name="debug">Set to true to see extracted text and parsing details. Example: ?debug=true</param>
    /// <returns>List of extracted transactions</returns>
    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Post(
        IFormFile file, 
        [FromQuery(Name = "debug")] bool debug = false)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Missing file.");
        }

        if (!file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only PDF allowed.");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        // Extract text and page count from PDF
        // PRIMARY METHOD: Get page count directly from PDF document structure (most reliable)
        // This reads the actual page count from the PDF, not from text parsing
        var extractionResult = await _extractor.ExtractTextWithPageCountAsync(ms);
        var raw = extractionResult.Text;
        var pageCount = extractionResult.PageCount; // From PDF structure - authoritative source
        
        var normalized = _normalizer.Normalize(raw);
        var transactions = _parser.Parse(normalized);

        // Extract issuer
        var issuer = _issuerExtractor.ExtractIssuer(normalized);

        // Enhanced duplicate detection with hash IDs and metadata
        var transactionsWithMetadata = _duplicateDetector.DetectAndMarkDuplicates(transactions);
        var uniqueTransactions = transactionsWithMetadata.Where(t => !t.IsDuplicate).ToList();
        var duplicates = transactionsWithMetadata.Where(t => t.IsDuplicate).ToList();
        var duplicateReport = _duplicateDetector.GetDuplicateReport(transactions);

        // Extract account information (use unique transactions for calculations)
        // Note: uniqueTransactions is already List<Transaction>, so we can use it directly
        var accountInfo = _accountInfoExtractor.ExtractAccountInfo(normalized, uniqueTransactions);

        // Extract statement metadata (pass page count from PDF)
        var metadata = _metadataExtractor.ExtractMetadata(normalized, pageCount);

        // Calculate period dates from transactions
        DateOnly periodStart = DateOnly.MinValue;
        DateOnly periodEnd = DateOnly.MinValue;
        string duration = "N/A";

        if (uniqueTransactions.Count > 0)
        {
            var dates = uniqueTransactions.Select(t => DateOnly.FromDateTime(t.Date)).OrderBy(d => d).ToList();
            periodStart = dates.First();
            periodEnd = dates.Last();

            // Calculate duration in months
            duration = CalculateDuration(periodStart, periodEnd);
        }

        // Debug mode: return extracted text and parsing details
        if (debug)
        {
            return Ok(new
            {
                rawTextLength = raw.Length,
                rawTextPreview = raw.Length > 500 ? raw.Substring(0, 500) + "..." : raw,
                normalizedLinesCount = normalized.Lines.Count,
                normalizedLinesPreview = normalized.Lines.Take(10).ToList(),
                transactionsFound = transactions.Count, // Raw parsed transactions (before duplicate removal)
                uniqueTransactionsCount = uniqueTransactions.Count, // After duplicate removal
                duplicatesCount = duplicates.Count,
                duplicates = duplicates,
                duplicateReport = duplicateReport,
                transactions = uniqueTransactions,
                transactionsWithMetadata = transactionsWithMetadata, // Includes hash IDs and duplicate flags
                allParsedTransactions = transactions, // ALL parsed transactions before duplicate removal
                issuer = issuer,
                periodStart = periodStart,
                periodEnd = periodEnd,
                duration = duration,
                transactionCount = uniqueTransactions.Count, // Final count (unique transactions only)
                accountInfo = accountInfo,
                metadata = metadata,
                // Analysis info
                analysis = new
                {
                    rawParsedCount = transactions.Count,
                    afterDuplicateRemoval = uniqueTransactions.Count,
                    duplicatesRemoved = duplicates.Count,
                    expectedFromPDF = "33-34 (depending on incomplete line handling)",
                    // Check for the missing transaction
                    missingTransactionCheck = transactions.Any(t => 
                        t.Date.ToString("dd/MM/yyyy") == "16/12/2025" && 
                        t.Description.Contains("PayShap", StringComparison.OrdinalIgnoreCase) &&
                        Math.Abs(t.Amount - (-100.00m)) < 0.01m) ? "FOUND in parsed transactions" : "NOT FOUND in parsed transactions",
                    transactionsOn16Dec = transactions.Where(t => t.Date.ToString("dd/MM/yyyy") == "16/12/2025").Select(t => new { t.Description, t.Amount, t.Fee, t.Category }).ToList(),
                    // Check transactions with fees
                    transactionsWithFees = transactions.Where(t => t.Fee.HasValue && t.Fee.Value > 0).Select(t => new { t.Date, t.Description, t.Amount, t.Fee }).ToList(),
                    transactionsWithoutFeesButShouldHave = transactions.Where(t => 
                        !t.Fee.HasValue && 
                        (t.Description.Contains("PayShap", StringComparison.OrdinalIgnoreCase) ||
                         t.Description.Contains("Payment", StringComparison.OrdinalIgnoreCase) ||
                         t.Description.Contains("Purchase", StringComparison.OrdinalIgnoreCase)) &&
                        t.Amount < 0).Select(t => new { t.Date, t.Description, t.Amount }).ToList()
                }
            });
        }

        var response = new TransactionResponse(
            issuer, 
            periodStart, 
            periodEnd, 
            duration, 
            uniqueTransactions.Count,
            accountInfo, 
            metadata, 
            uniqueTransactions);
        return Ok(response);
    }

    private static string CalculateDuration(DateOnly start, DateOnly end)
    {
        if (start == DateOnly.MinValue || end == DateOnly.MinValue)
            return "N/A";

        var months = (end.Year - start.Year) * 12 + (end.Month - start.Month);
        
        if (months == 0)
        {
            var days = end.DayNumber - start.DayNumber;
            return days == 0 ? "Same day" : $"{days} day{(days == 1 ? "" : "s")}";
        }
        else if (months == 1)
        {
            return "1 month";
        }
        else
        {
            // Calculate years and remaining months
            var years = months / 12;
            var remainingMonths = months % 12;

            if (years == 0)
            {
                return $"{months} months";
            }
            else if (remainingMonths == 0)
            {
                return years == 1 ? "1 year" : $"{years} years";
            }
            else
            {
                var yearStr = years == 1 ? "1 year" : $"{years} years";
                var monthStr = remainingMonths == 1 ? "1 month" : $"{remainingMonths} months";
                return $"{yearStr}, {monthStr}";
            }
        }
    }
}

