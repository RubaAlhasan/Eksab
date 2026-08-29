using System;

namespace Eksabli.Account;

[Serializable]
public class MustChangePasswordCacheItem
{
    public bool MustChangePassword { get; set; }
}
