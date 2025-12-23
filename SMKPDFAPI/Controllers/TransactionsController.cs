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

        var extractionResult = await _extractor.ExtractTextWithPageCountAsync(ms);
        var raw = extractionResult.Text;
        var pageCount = extractionResult.PageCount;
        
        var normalized = _normalizer.Normalize(raw);
        var transactions = _parser.Parse(normalized);

        // Extract issuer
        var issuer = _issuerExtractor.ExtractIssuer(normalized);

        var transactionsWithMetadata = _duplicateDetector.DetectAndMarkDuplicates(transactions);
        var uniqueTransactions = transactionsWithMetadata.Where(t => !t.IsDuplicate).ToList();
        var duplicates = transactionsWithMetadata.Where(t => t.IsDuplicate).ToList();
        var duplicateReport = _duplicateDetector.GetDuplicateReport(transactions);

        var accountInfo = _accountInfoExtractor.ExtractAccountInfo(normalized, uniqueTransactions);
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
                transactionsFound = transactions.Count,
                uniqueTransactionsCount = uniqueTransactions.Count,
                duplicatesCount = duplicates.Count,
                duplicates = duplicates,
                duplicateReport = duplicateReport,
                transactions = uniqueTransactions,
                transactionsWithMetadata = transactionsWithMetadata,
                allParsedTransactions = transactions,
                issuer = issuer,
                periodStart = periodStart,
                periodEnd = periodEnd,
                duration = duration,
                transactionCount = uniqueTransactions.Count,
                accountInfo = accountInfo,
                metadata = metadata,
                analysis = new
                {
                    rawParsedCount = transactions.Count,
                    afterDuplicateRemoval = uniqueTransactions.Count,
                    duplicatesRemoved = duplicates.Count
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

