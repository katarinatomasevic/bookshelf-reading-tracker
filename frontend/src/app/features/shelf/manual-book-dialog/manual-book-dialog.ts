import { Component, inject, model, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TextareaModule } from 'primeng/textarea';
import { ShelfItem } from '../../../core/models/shelf.model';
import { ShelfService } from '../shelf.service';

/**
 * Fallback for books Open Library does not have: creates the book and shelves it in one step.
 * It lives on the shelf page as a dialog so adding a book never costs the user their place.
 */
@Component({
  selector: 'app-manual-book-dialog',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    MessageModule,
    TextareaModule,
  ],
  templateUrl: './manual-book-dialog.html',
  styleUrl: './manual-book-dialog.scss',
})
export class ManualBookDialog {
  private readonly fb = inject(FormBuilder);
  private readonly shelfService = inject(ShelfService);

  readonly visible = model(false);
  readonly created = output<ShelfItem>();

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required]],
    author: [''],
    pageCount: [null as number | null, [Validators.min(1)]],
    subjects: [''],
    description: [''],
  });

  onCancel(): void {
    this.visible.set(false);
  }

  /** Reached by ✕ and Esc as well as Cancel, so resetting belongs here rather than in onCancel. */
  onHide(): void {
    this.form.reset();
    this.submitting.set(false);
    this.errorMessage.set(null);
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const value = this.form.getRawValue();

    this.shelfService
      .addManual({
        title: value.title.trim(),
        author: this.optional(value.author),
        description: this.optional(value.description),
        pageCount: value.pageCount,
        subjects: this.parseSubjects(value.subjects),
      })
      .subscribe({
        next: (item) => {
          this.submitting.set(false);
          this.created.emit(item);
          this.visible.set(false);
        },
        error: (err) => {
          this.submitting.set(false);
          this.errorMessage.set(err.error?.title ?? 'Could not add the book. Please try again.');
        },
      });
  }

  private optional(value: string): string | null {
    const trimmed = value.trim();
    return trimmed.length > 0 ? trimmed : null;
  }

  /** One comma-separated field rather than a tag editor: subjects are free text anyway. */
  private parseSubjects(value: string): string[] {
    return value
      .split(',')
      .map((subject) => subject.trim())
      .filter((subject) => subject.length > 0)
      .slice(0, 15);
  }
}
