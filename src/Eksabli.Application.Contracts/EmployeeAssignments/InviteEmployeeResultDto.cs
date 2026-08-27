namespace Eksabli.EmployeeAssignments;

// Returned only from InviteAsync — the one moment a temporary password is ever surfaced (never part of
// EmployeeAssignmentDto itself, which GetListAsync/UpdateAsync also return — keeping this a separate
// wrapper means there's no risk of a stale temp password leaking out of an unrelated list/read call).
// The invited account has IdentityUser.ShouldChangePasswordOnNextLogin = true, so this password is only
// ever meant to get the new staff member logged in once, not to be a real long-term credential — the
// inviter is expected to hand it to them directly (Slack, in person, whatever), there's no email step.
public class InviteEmployeeResultDto
{
    public EmployeeAssignmentDto Assignment { get; set; } = null!;

    public string TemporaryPassword { get; set; } = string.Empty;
}
