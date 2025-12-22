using System.Text.RegularExpressions;

namespace SMKPDFAPI.Parsing;

public class BankIssuerExtractor : IIssuerExtractor
{
    // Common bank names and their variations
    private static readonly Dictionary<string, string> BankPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        { @"\bCapitec\s+Bank\b", "Capitec Bank" },
        { @"\bCapitec\b", "Capitec Bank" },
        { @"\bStandard\s+Bank\b", "Standard Bank" },
        { @"\bFirst\s+National\s+Bank\b", "First National Bank" },
        { @"\bFNB\b", "First National Bank" },
        { @"\bNedbank\b", "Nedbank" },
        { @"\bAbsa\s+Bank\b", "Absa Bank" },
        { @"\bAbsa\b", "Absa Bank" },
        { @"\bAfrican\s+Bank\b", "African Bank" },
        { @"\bInvestec\b", "Investec" },
        { @"\bDiscovery\s+Bank\b", "Discovery Bank" },
        { @"\bTyme\s+Bank\b", "Tyme Bank" },
        { @"\bBank\s+of\s+America\b", "Bank of America" },
        { @"\bChase\b", "Chase" },
        { @"\bWells\s+Fargo\b", "Wells Fargo" },
        { @"\bCitibank\b", "Citibank" },
        { @"\bAmerican\s+Express\b", "American Express" },
        { @"\bAmex\b", "American Express" },
        { @"\bVisa\b", "Visa" },
        { @"\bMastercard\b", "Mastercard" }
    };

    public string ExtractIssuer(StatementText text)
    {
        // Check first 50 lines (usually contains bank name in header)
        var headerLines = text.Lines.Take(50).ToList();
        var headerText = string.Join(" ", headerLines);

        // Try to find bank name patterns
        foreach (var pattern in BankPatterns)
        {
            if (Regex.IsMatch(headerText, pattern.Key, RegexOptions.IgnoreCase))
            {
                return pattern.Value;
            }
        }

        // Fallback: look for common bank statement keywords
        var keywords = new[] { "Statement", "Account", "Transaction", "Balance" };
        var hasStatementKeywords = keywords.Any(k => headerText.Contains(k, StringComparison.OrdinalIgnoreCase));

        if (hasStatementKeywords)
        {
            // Try to extract any capitalized word followed by "Bank" or "Statement"
            var bankMatch = Regex.Match(headerText, @"\b([A-Z][a-zA-Z]+(?:\s+[A-Z][a-zA-Z]+)*)\s+(?:Bank|Statement)\b", RegexOptions.IgnoreCase);
            if (bankMatch.Success && bankMatch.Groups.Count > 1)
            {
                var potentialBank = bankMatch.Groups[1].Value.Trim();
                if (potentialBank.Length > 2 && potentialBank.Length < 50)
                {
                    return potentialBank;
                }
            }
        }

        return "Unknown";
    }
}

