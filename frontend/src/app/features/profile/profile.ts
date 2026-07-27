import { Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { UserService } from '../../core/services/user.service';
import { AuthService } from '../../core/services/auth.service';
import { PASSWORD_PATTERN, PASSWORD_REQUIREMENTS_HINT } from '../../core/validators/password-policy';

function passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
  const newPassword = group.get('newPassword')?.value;
  const confirmPassword = group.get('confirmPassword')?.value;
  return newPassword === confirmPassword ? null : { passwordsMismatch: true };
}

@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, InputTextModule, PasswordModule, ButtonModule, MessageModule],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly userService = inject(UserService);
  private readonly authService = inject(AuthService);

  protected readonly displayNameSubmitting = signal(false);
  protected readonly displayNameMessage = signal<string | null>(null);
  protected readonly displayNameError = signal<string | null>(null);

  /** Last value confirmed by the backend — used for the password save so an unsaved,
   *  still-dirty edit in the display name field is never smuggled in as a side effect. */
  private savedDisplayName = '';

  protected readonly passwordSubmitting = signal(false);
  protected readonly passwordMessage = signal<string | null>(null);
  protected readonly passwordError = signal<string | null>(null);
  protected readonly passwordRequirementsHint = PASSWORD_REQUIREMENTS_HINT;

  protected readonly displayNameForm = this.fb.nonNullable.group({
    displayName: ['', [Validators.required]],
  });

  protected readonly passwordForm = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.pattern(PASSWORD_PATTERN)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatchValidator },
  );

  ngOnInit(): void {
    this.userService.getMe().subscribe((profile) => {
      this.savedDisplayName = profile.displayName;
      this.displayNameForm.patchValue({ displayName: profile.displayName });
    });
  }

  onSaveDisplayName(): void {
    if (this.displayNameForm.invalid) {
      this.displayNameForm.markAllAsTouched();
      return;
    }

    this.displayNameSubmitting.set(true);
    this.displayNameMessage.set(null);
    this.displayNameError.set(null);

    this.userService
      .updateMe({
        displayName: this.displayNameForm.getRawValue().displayName,
        currentPassword: null,
        newPassword: null,
      })
      .subscribe({
        next: (profile) => {
          this.savedDisplayName = profile.displayName;
          this.authService.updateDisplayName(profile.displayName);
          this.displayNameSubmitting.set(false);
          this.displayNameMessage.set('Display name updated.');
        },
        error: (err) => {
          this.displayNameSubmitting.set(false);
          this.displayNameError.set(err.error?.title ?? 'Could not update display name.');
        },
      });
  }

  onSavePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.passwordSubmitting.set(true);
    this.passwordMessage.set(null);
    this.passwordError.set(null);

    const { currentPassword, newPassword } = this.passwordForm.getRawValue();

    this.userService
      .updateMe({
        displayName: this.savedDisplayName,
        currentPassword,
        newPassword,
      })
      .subscribe({
        next: () => {
          this.passwordSubmitting.set(false);
          this.passwordMessage.set('Password updated.');
          this.passwordForm.reset();
        },
        error: (err) => {
          this.passwordSubmitting.set(false);
          this.passwordError.set(err.error?.title ?? 'Could not update password.');
        },
      });
  }
}
