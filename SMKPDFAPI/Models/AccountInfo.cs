namespace SMKPDFAPI.Models;

public record AccountInfo(
    string? AccountNumber = null,
    string? AccountHolderName = null,
    string? AccountType = null,
    decimal? OpeningBalance = null,
    decimal? ClosingBalance = null,
    decimal TotalCredits = 0,
    decimal TotalDebits = 0,
    decimal? TotalInterestEarned = null,
    decimal? TotalInterestCharged = null);

