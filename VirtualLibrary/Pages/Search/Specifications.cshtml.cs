using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VirtualLibrary.Services;

namespace VirtualLibrary.Pages.Search
{
    [AllowAnonymous]
    public class SpecificationsModel : PageModel
    {
        private readonly LuceneSpecificationSearchService _luceneSearchService;

        public SpecificationsModel(LuceneSpecificationSearchService luceneSearchService)
        {
            _luceneSearchService = luceneSearchService;
        }

        [BindProperty(SupportsGet = true)]
        public string? q { get; set; }

        [BindProperty(SupportsGet = true)]
        public string sort { get; set; } = "score_desc";

        public IReadOnlyList<LuceneSpecificationSearchService.SpecificationSearchResult> Results { get; private set; }
            = Array.Empty<LuceneSpecificationSearchService.SpecificationSearchResult>();

        public async Task OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                Results = Array.Empty<LuceneSpecificationSearchService.SpecificationSearchResult>();
                return;
            }

            if (sort != "score_asc" && sort != "score_desc")
            {
                sort = "score_desc";
            }

            Results = await _luceneSearchService.SearchAsync(q, sort, 50);
        }
    }
}