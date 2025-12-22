namespace SMKPDFAPI.Pdf;

public interface IPdfTextExtractor
{
    Task<string> ExtractTextAsync(Stream pdfStream);
    Task<PdfExtractionResult> ExtractTextWithPageCountAsync(Stream pdfStream);
}

