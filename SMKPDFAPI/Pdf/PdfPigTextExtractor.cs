using System.Text;
using UglyToad.PdfPig;

namespace SMKPDFAPI.Pdf;

public class PdfPigTextExtractor : IPdfTextExtractor
{
    public Task<PdfExtractionResult> ExtractTextWithPageCountAsync(Stream pdfStream)
    {
        var builder = new StringBuilder();
        int pageCount = 0;

        using var document = PdfDocument.Open(pdfStream);
        foreach (var page in document.GetPages())
        {
            pageCount++;
            builder.AppendLine(page.Text);
        }

        return Task.FromResult(new PdfExtractionResult(builder.ToString(), pageCount));
    }
}

