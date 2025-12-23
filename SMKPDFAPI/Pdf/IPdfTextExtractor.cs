namespace SMKPDFAPI.Pdf;

public interface IPdfTextExtractor
{
    Task<PdfExtractionResult> ExtractTextWithPageCountAsync(Stream pdfStream);
}

