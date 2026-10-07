using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Reviews;

// PagedResultDto already carries Items + TotalCount; AverageRating is computed over every review at
// this business, not just the items on the current page, so a client paging through reviews still
// shows the one true average rather than recomputing a wrong one from a single page.
public class ReviewListResultDto : PagedResultDto<ReviewDto>
{
    public double AverageRating { get; set; }

    public ReviewListResultDto()
    {
    }

    public ReviewListResultDto(long totalCount, List<ReviewDto> items, double averageRating)
        : base(totalCount, items)
    {
        AverageRating = averageRating;
    }
}
