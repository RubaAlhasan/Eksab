// OTP sign-in creates the account with a generated `<guid>@otp.eksabli.local` address, because ABP's identity
// user requires an email field. Members never type it, so the profile must not present it as their email.
const PLACEHOLDER_EMAIL_DOMAIN = '@otp.eksabli.local';

export function isPlaceholderEmail(email: string | null | undefined): boolean {
  return !!email && email.toLowerCase().endsWith(PLACEHOLDER_EMAIL_DOMAIN);
}

// Admin and business forms accept a link typed with or without its scheme, but a link shown to a member must
// be absolute, or it opens relative to the app and goes nowhere.
export function toExternalHref(url: string): string {
  return /^https?:\/\//i.test(url) ? url : `https://${url}`;
}

// The address as a member reads it: the scheme and any trailing slash dropped.
export function displayUrl(url: string): string {
  return url.replace(/^https?:\/\//i, '').replace(/\/+$/, '');
}
