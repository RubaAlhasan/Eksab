using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Reviews;

public class ReviewDto : AuditedEntityDto<Guid>
{
    public Guid CustomerId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    // "First name + last initial" (e.g. "Sara K."), computed server-side from CustomerProfile — never
    // the full last name, since this is shown publicly on the business's own page. Null when the
    // reviewer has no first name on file; the client shows its own localized "Member" fallback, the
    // same way BusinessPanel:Redemption:UnnamedCustomer already does for an unnamed member elsewhere.
    public string? ReviewerName { get; set; }
}
