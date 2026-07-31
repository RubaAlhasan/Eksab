using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Eksabli.Authors;

public class Author : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; }

    public DateTime BirthDate { get; set; }

    public string? ShortBio { get; set; }
}
