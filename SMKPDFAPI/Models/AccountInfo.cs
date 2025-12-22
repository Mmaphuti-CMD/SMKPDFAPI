namespace SMKPDFAPI.Models;

public record AccountInfo(
    string? AccountNumber = null,
    string? AccountHolderName = null,
    string? AccountType = null,
    decimal? OpeningBalance = null,
    decimal? ClosingBalance = null,
    decimal? TotalCredits = null,
    decimal? TotalDebits = null,
    decimal? TotalFees = null,
    decimal? InterestEarned = null,
    decimal? InterestCharged = null);

