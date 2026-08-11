namespace Eksabli.Platform;

// Admin Portal > Users (cross-tenant user directory, Host-only). Only two real kinds of person show up
// there — a Host-realm customer (CustomerProfile) or tenant-realm business staff (EmployeeAssignment) —
// see AdminUserAppService for how each is resolved. Platform staff accounts (the seeded Host admin,
// Support Agent, Billing Admin, ...) are deliberately NOT a third value here; they're managed via the
// stock Identity > Users page, not this directory (matches prototype/admin/users.html's own scope).
public enum AdminUserType
{
    Customer = 0,
    Staff = 1
}
