using System.ComponentModel.DataAnnotations;

namespace Eksabli.Engagement;

public class CreateUpdateAchievementDto
{
    [Required]
    [StringLength(AchievementConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(AchievementConsts.MaxCriteriaJsonLength)]
    public string? CriteriaJson { get; set; }
}
