import { AbstractControl, FormArray, FormGroup } from '@angular/forms';
import { problemOf } from '../core/api.service';

/**
 * Copies server field errors (e.g. "lines[0].pcsQuantity") onto matching form controls as { server: message }.
 * Returns the general message to show to the user.
 */
export function applyServerErrors(form: FormGroup, error: unknown): string {
  const problem = problemOf(error);
  for (const [path, messages] of Object.entries(problem.errors ?? {})) {
    const control = findControl(form, path);
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: messages[0] });
      control.markAsTouched();
    }
  }
  return problem.title ?? 'Please check the form.';
}

function findControl(root: AbstractControl, path: string): AbstractControl | null {
  let current: AbstractControl | null = root;
  for (const part of path.split('.')) {
    if (!current) return null;
    const match = /^(\w+)\[(\d+)\]$/.exec(part);
    if (match) {
      const arr: AbstractControl | null = current.get(match[1]);
      current = arr instanceof FormArray ? arr.at(Number(match[2])) : null;
    } else {
      current = current instanceof FormGroup ? current.get(part) : null;
    }
  }
  return current;
}

/** Human readable message for a control's first error. */
export function controlError(control: AbstractControl | null, label = 'This field'): string {
  const e = control?.errors;
  if (!e) return '';
  if (e['server']) return e['server'];
  if (e['required']) return `${label} is required.`;
  if (e['min']) return `${label} must be at least ${e['min'].min}.`;
  if (e['max']) return `${label} must be at most ${e['max'].max}.`;
  if (e['email']) return 'Enter a valid email address.';
  if (e['maxlength']) return `${label} must be at most ${e['maxlength'].requiredLength} characters.`;
  if (e['minlength']) return `${label} must be at least ${e['minlength'].requiredLength} characters.`;
  if (e['pattern']) return `${label} is not in the right format.`;
  if (e['bdMobile']) return 'Enter a valid Bangladesh mobile number, e.g. 01712345678.';
  if (e['password']) return 'At least 8 characters with a letter and a number.';
  if (e['mismatch']) return 'Passwords do not match.';
  return `${label} is not valid.`;
}

export const BD_MOBILE_PATTERN = /^(?:\+?88)?01[3-9]\d{8}$/;

export function bdMobileValidator(control: AbstractControl) {
  const value = String(control.value ?? '').replace(/[\s-]/g, '');
  if (!value) return null;
  return BD_MOBILE_PATTERN.test(value) ? null : { bdMobile: true };
}

export function passwordValidator(control: AbstractControl) {
  const v = String(control.value ?? '');
  if (!v) return null;
  return v.length >= 8 && /[A-Za-z]/.test(v) && /\d/.test(v) ? null : { password: true };
}
