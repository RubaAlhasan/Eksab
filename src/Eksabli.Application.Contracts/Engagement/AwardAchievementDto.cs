using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Engagement;

public class AwardAchievementDto
{
    [Required]
    public Guid MembershipId { get; set; }

    [Required]
    public Guid AchievementId { get; set; }
}
