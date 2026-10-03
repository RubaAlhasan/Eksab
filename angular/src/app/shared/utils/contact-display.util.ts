// OTP sign-in creates the account with a generated `<guid>@otp.eksabli.local` address, because ABP's identity
// user requires an email field. Members never type it, so the profile must not present it as their email.
const PLACEHOLDER_EMAIL_DOMAIN = '@otp.eksabli.local';

export function isPlaceholderEmail(email: string | null | undefined): boolean {
  return !!email && email.toLowerCase().endsWith(PLACEHOLDER_EMAIL_DOMAIN);
}
