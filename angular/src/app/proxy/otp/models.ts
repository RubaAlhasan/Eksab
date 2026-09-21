import type { CustomerGender } from '../customer-profiles/customer-gender.enum';

export interface RequestOtpDto {
  phoneNumber: string;
}

// Hand-added — OtpController already exposes POST /api/app/otp/register (see its own comment: "Call
// before completing sign-in via POST /connect/token"), but the generated proxy predates it. Regenerate
// via `abp generate-proxy -t ng` to replace this with the real generated version once convenient; shape
// matches RegisterCustomerDto (Eksabli.Otp namespace) field-for-field.
export interface RegisterCustomerDto {
  phoneNumber: string;
  firstName: string;
  lastName: string;
  email?: string | null;
  dateOfBirth?: string | null;
  gender?: CustomerGender;
  password: string;
}
