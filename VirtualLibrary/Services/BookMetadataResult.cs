namespace VirtualLibrary.Services
{
    public class BookMetadataResult
    {
        public string? Publisher { get; set; }
        public int? PublishedYear { get; set; }
        public int? PageCount { get; set; }
        public string? Language { get; set; }
        public decimal? Rating { get; set; }
        public string? Description { get; set; }
    }
}