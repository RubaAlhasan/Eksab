using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Reports;

public class ReportPeriodDto
{
    [Required]
    public DateTime From { get; set; }

    [Required]
    public DateTime To { get; set; }
}
