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

    public TransactionsController(IPdfTextExtractor extractor, IStatementNormalizer normalizer, ITransactionParser parser)
    {
        _extractor = extractor;
        _normalizer = normalizer;
        _parser = parser;
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
    /// <param name="debug">Set to true to see extracted text and parsing details</param>
    /// <returns>List of extracted transactions</returns>
    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Post(
        IFormFile file, 
        [FromQuery] bool debug = false)
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

        var raw = await _extractor.ExtractTextAsync(ms);
        var normalized = _normalizer.Normalize(raw);
        var transactions = _parser.Parse(normalized);

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
                transactions = transactions,
                issuer = "Unknown",
                periodStart = DateOnly.MinValue,
                periodEnd = DateOnly.MinValue
            });
        }

        var response = new TransactionResponse("Unknown", DateOnly.MinValue, DateOnly.MinValue, transactions);
        return Ok(response);
    }
}

