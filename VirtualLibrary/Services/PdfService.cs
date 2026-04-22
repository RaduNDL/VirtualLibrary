using UglyToad.PdfPig;

namespace VirtualLibrary.Services
{
    public class PdfService
    {
        public Task<string> ExtractTextAsync(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath))
                throw new ArgumentException("PDF path is required.", nameof(pdfPath));

            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("PDF file not found.", pdfPath);

            var textParts = new List<string>();

            using (var document = PdfDocument.Open(pdfPath))
            {
                foreach (var page in document.GetPages())
                {
                    if (!string.IsNullOrWhiteSpace(page.Text))
                    {
                        textParts.Add(page.Text);
                    }
                }
            }

            return Task.FromResult(string.Join(Environment.NewLine, textParts));
        }
    }
}