using System.Globalization;
using System.Text.RegularExpressions;
using SMKPDFAPI.Models;

namespace SMKPDFAPI.Parsing;

public class BankStatementMetadataExtractor : IStatementMetadataExtractor
{
    public StatementMetadata ExtractMetadata(StatementText text, int? pageCount = null)
    {
        // Check first 50 lines and last 20 lines for metadata (usually in header/footer)
        var headerLines = text.Lines.Take(50).ToList();
        var footerLines = text.Lines.Skip(Math.Max(0, text.Lines.Count - 20)).ToList();
        var headerText = string.Join(" ", headerLines);
        var footerText = string.Join(" ", footerLines);
        var allText = string.Join(" ", text.Lines);

        var statementDate = ExtractStatementDate(headerText, footerText);
        var statementNumber = ExtractStatementNumber(headerText, footerText);
        
        // PRIMARY METHOD: Use page count from PDF document structure (most reliable)
        // FALLBACK: If not available, try parsing "Page X of Y" from text
        // The PDF structure method is preferred because:
        // - It's authoritative (reads actual PDF page count)
        // - Works even if page numbers aren't in the text
        // - Faster (no regex parsing needed)
        // - More reliable (doesn't depend on text formatting)
        var totalPages = pageCount ?? ExtractTotalPages(allText, footerText);

        return new StatementMetadata(
            StatementDate: statementDate,
            StatementNumber: statementNumber,
            TotalPages: totalPages
        );
    }

    private static DateOnly? ExtractStatementDate(string headerText, string footerText)
    {
        // Look for patterns like: "Statement Date: 01/12/2025", "Generated: 01 Dec 2025", "Date: 2025-12-01"
        var datePatterns = new[]
        {
            new Regex(@"(?:Statement\s+Date|Generated\s+Date|Date\s+Generated|Date)\s*:?\s*(\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4})", RegexOptions.IgnoreCase),
            new Regex(@"(?:Statement\s+Date|Generated\s+Date|Date\s+Generated|Date)\s*:?\s*(\d{1,2}\s+[A-Z][a-z]{2,9}\s+\d{4})", RegexOptions.IgnoreCase),
            new Regex(@"(?:Statement\s+Date|Generated\s+Date|Date\s+Generated|Date)\s*:?\s*(\d{4}[-/]\d{1,2}[-/]\d{1,2})", RegexOptions.IgnoreCase)
        };

        var textsToSearch = new[] { headerText, footerText };

        foreach (var text in textsToSearch)
        {
            foreach (var pattern in datePatterns)
            {
                var match = pattern.Match(text);
                if (match.Success && match.Groups.Count > 1)
                {
                    var dateStr = match.Groups[1].Value.Trim();
                    
                    // Try parsing with common formats
                    var formats = new[]
                    {
                        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy",
                        "dd-MM-yyyy", "d-M-yyyy", "dd-MM-yy", "d-M-yy",
                        "yyyy-MM-dd", "yyyy/MM/dd",
                        "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy", "d MMMM yyyy"
                    };

                    if (DateTime.TryParseExact(dateStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        return DateOnly.FromDateTime(date);
                    }

                    // Fallback to standard parsing
                    if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    {
                        return DateOnly.FromDateTime(parsedDate);
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractStatementNumber(string headerText, string footerText)
    {
        // Look for patterns like: "Statement Number: 12345", "Statement #: 12345", "Stmt No: 12345"
        var numberPatterns = new[]
        {
            new Regex(@"(?:Statement\s+(?:Number|No\.?|#)|Stmt\s+(?:No\.?|Number|#))\s*:?\s*([A-Z0-9\-]+)", RegexOptions.IgnoreCase),
            new Regex(@"Statement\s*:?\s*([A-Z0-9\-]{3,})", RegexOptions.IgnoreCase),
            new Regex(@"Ref(?:erence|\.?)\s*:?\s*([A-Z0-9\-]{3,})", RegexOptions.IgnoreCase)
        };

        var textsToSearch = new[] { headerText, footerText };

        foreach (var text in textsToSearch)
        {
            foreach (var pattern in numberPatterns)
            {
                var match = pattern.Match(text);
                if (match.Success && match.Groups.Count > 1)
                {
                    var statementNum = match.Groups[1].Value.Trim();
                    // Validate it looks like a statement number (alphanumeric, 3-20 chars)
                    if (statementNum.Length >= 3 && statementNum.Length <= 20 && 
                        Regex.IsMatch(statementNum, @"^[A-Z0-9\-]+$", RegexOptions.IgnoreCase))
                    {
                        return statementNum;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// FALLBACK METHOD: Extracts total pages by parsing "Page X of Y" from text.
    /// 
    /// COMPARISON OF APPROACHES:
    /// 
    /// 1. Reading from PDF structure (PRIMARY - used in controller):
    ///    - More reliable: Gets actual page count from PDF document structure
    ///    - Faster: No regex parsing needed
    ///    - Works even if page numbers aren't in text
    ///    - Authoritative: PDF structure is the source of truth
    /// 
    /// 2. Parsing from text (FALLBACK - this method):
    ///    - Less reliable: Depends on text being present and correctly formatted
    ///    - Slower: Requires regex pattern matching
    ///    - May fail if page numbers are missing or in unexpected format
    ///    - Only used when PDF structure method is unavailable
    /// 
    /// Your PDF has "Page 1 of 2" and "Page 2 of 2" at bottom right corner.
    /// Both methods would work, but PDF structure method is preferred.
    /// </summary>
    private static int? ExtractTotalPages(string allText, string footerText)
    {
        // Look for patterns like: "Page 1 of 2", "Page 1/2", "1 of 2 pages"
        // Your PDF format: "Page 1 of 2" and "Page 2 of 2" at bottom right
        var pagePatterns = new[]
        {
            new Regex(@"Page\s+\d+\s+of\s+(\d+)", RegexOptions.IgnoreCase), // "Page 1 of 2" - matches your format
            new Regex(@"Page\s+\d+\s*/\s*(\d+)", RegexOptions.IgnoreCase),   // "Page 1/2"
            new Regex(@"\d+\s+of\s+(\d+)\s+pages?", RegexOptions.IgnoreCase) // "1 of 2 pages"
        };

        // Prefer footer text (pages usually in footer, like bottom right corner in your PDF)
        var textsToSearch = new[] { footerText, allText };

        foreach (var text in textsToSearch)
        {
            // Try the first pattern which matches "Page X of Y" format (your PDF format)
            var match = Regex.Match(text, @"Page\s+\d+\s+of\s+(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                if (int.TryParse(match.Groups[1].Value, out var totalPages) && totalPages > 0 && totalPages <= 1000)
                {
                    return totalPages;
                }
            }

            // Try other patterns as fallback
            foreach (var pattern in pagePatterns.Skip(1))
            {
                match = pattern.Match(text);
                if (match.Success && match.Groups.Count > 1)
                {
                    if (int.TryParse(match.Groups[1].Value, out var pages) && pages > 0 && pages <= 1000)
                    {
                        return pages;
                    }
                }
            }
        }

        return null;
    }
}

