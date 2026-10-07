using System.ComponentModel.DataAnnotations;

namespace Eksabli.Reviews;

public class CreateUpdateReviewDto
{
    [Range(ReviewConsts.MinRating, ReviewConsts.MaxRating)]
    public int Rating { get; set; }

    [StringLength(ReviewConsts.MaxCommentLength)]
    public string? Comment { get; set; }
}
