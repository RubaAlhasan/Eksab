namespace Eksabli.Dashboards;

// One cell of the peak-hours grid, in the business's own clock. Activity counts purchase awards plus completed Buy Now
// sales. The grid always has 7 x 24 cells, zero-filled, so the UI never has to guess which cells are missing.
public class PeakHourCellDto
{
    // 0 = Sunday ... 6 = Saturday, the same numbering as System.DayOfWeek.
    public int DayOfWeek { get; set; }

    // 0 to 23, local.
    public int Hour { get; set; }

    public int Activity { get; set; }
}
