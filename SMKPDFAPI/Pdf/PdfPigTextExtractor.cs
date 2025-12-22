using System.Text;
using UglyToad.PdfPig;

namespace SMKPDFAPI.Pdf;

public class PdfPigTextExtractor : IPdfTextExtractor
{
    public Task<string> ExtractTextAsync(Stream pdfStream)
    {
        var builder = new StringBuilder();

        using var document = PdfDocument.Open(pdfStream);
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return Task.FromResult(builder.ToString());
    }

    public Task<PdfExtractionResult> ExtractTextWithPageCountAsync(Stream pdfStream)
    {
        var builder = new StringBuilder();
        int pageCount = 0;

        using var document = PdfDocument.Open(pdfStream);
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
            pageCount++;
        }

        return Task.FromResult(new PdfExtractionResult(builder.ToString(), pageCount));
    }
}

