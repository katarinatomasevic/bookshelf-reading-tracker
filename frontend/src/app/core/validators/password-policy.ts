/**
 * Mirrors backend/Bookshelf.Application/Common/PasswordPolicy.cs exactly — keep both in
 * sync if the rule ever changes, so the frontend hint never promises something the
 * backend doesn't actually enforce (or vice versa).
 */
export const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$/;

export const PASSWORD_REQUIREMENTS_HINT =
  'At least 8 characters, with an uppercase letter, a lowercase letter, a number, and a special character.';
