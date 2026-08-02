import { Component, HostListener, effect, inject, input, model, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { RatingModule } from 'primeng/rating';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ReadingStatus, ShelfItem, UpdateShelfItemRequest } from '../../../core/models/shelf.model';
import { ShelfService } from '../shelf.service';

/**
 * The shelf entry as one editable form rather than a read-only view with an edit mode: every
 * field is live, and a single "Save changes" writes them all in one PATCH. A modal instead of a
 * page because users change status for several books in a row and a page would cost them their
 * scroll position each time.
 *
 * Nothing is ever saved implicitly — closing the dialog in any way (✕, Cancel, Esc, backdrop)
 * goes through {@link requestClose}, which asks before throwing edits away.
 */
@Component({
  selector: 'app-book-detail-modal',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    ConfirmDialogModule,
    DialogModule,
    InputTextModule,
    MessageModule,
    RatingModule,
    SelectModule,
    TextareaModule,
  ],
  providers: [ConfirmationService],
  templateUrl: './book-detail-modal.html',
  styleUrl: './book-detail-modal.scss',
})
export class BookDetailModal {
  private readonly fb = inject(FormBuilder);
  private readonly shelfService = inject(ShelfService);
  private readonly confirmationService = inject(ConfirmationService);

  readonly item = input<ShelfItem | null>(null);
  readonly visible = model(false);
  readonly removed = output<string>();

  protected readonly saving = signal(false);
  protected readonly removing = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  /** Esc and backdrop clicks must not reach the dialog while a confirmation sits on top of it. */
  private readonly confirming = signal(false);

  /** Mirrors the backend's own cap; shown to the user so the limit is never a surprise on save. */
  protected readonly noteMaxLength = 2000;

  protected readonly statusOptions = [
    { label: 'Want to read', value: ReadingStatus.WantToRead },
    { label: 'Reading', value: ReadingStatus.Reading },
    { label: 'Read', value: ReadingStatus.Read },
  ];

  protected readonly form = this.fb.nonNullable.group({
    status: [ReadingStatus.WantToRead],
    rating: [0],
    note: [''],
    startedAt: [''],
    finishedAt: [''],
    pageCount: [null as number | null],
  });

  constructor() {
    // The parent looks the item up in the shelf signal by id, so this also runs after a save:
    // the form is refilled from what the server actually stored and goes pristine again.
    effect(() => this.fillForm(this.item()));
  }

  /**
   * Refills the form from the stored entry and marks it pristine. Also runs on close, so
   * discarded edits are really gone when the same book is opened again.
   */
  private fillForm(item: ShelfItem | null): void {
    if (!item) {
      return;
    }

    this.form.reset({
      status: item.status,
      rating: item.rating ?? 0,
      note: item.note ?? '',
      startedAt: item.startedAt ?? '',
      finishedAt: item.finishedAt ?? '',
      pageCount: item.pageCount,
    });
    this.errorMessage.set(null);
  }

  protected coverUrl(item: ShelfItem): string | null {
    return item.coverId ? `https://covers.openlibrary.org/b/id/${item.coverId}-M.jpg` : null;
  }

  /** Manually added books have no Open Library key, so they open by our own id. */
  protected detailsLink(item: ShelfItem): string {
    return `/books/${item.openLibraryId ?? item.bookId}`;
  }

  /**
   * Page count belongs to the shared Book row, so it can only be filled in where Open Library
   * left a gap — overwriting it would silently change the book for every other user.
   */
  protected canEditPageCount(item: ShelfItem): boolean {
    return item.pageCount === null;
  }

  /** 0 is how the backend is told to drop the rating; the stars themselves cannot go back to it. */
  protected clearRating(): void {
    this.form.controls.rating.setValue(0);
    this.form.controls.rating.markAsDirty();
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.visible() && !this.confirming()) {
      this.requestClose();
    }
  }

  /**
   * The dialog runs with `closable=false`, which switches off PrimeNG's own ✕, Esc and backdrop
   * handling. Backdrop clicks are picked up here instead, matched by our own mask class so the
   * confirmation dialog's mask cannot trigger it.
   */
  @HostListener('document:mousedown', ['$event'])
  protected onDocumentMouseDown(event: MouseEvent): void {
    if (!this.visible() || this.confirming()) {
      return;
    }

    const target = event.target as HTMLElement | null;
    if (target?.classList.contains('shelf-modal-mask')) {
      this.requestClose();
    }
  }

  /** The single exit path: ✕, Cancel, Esc and backdrop all land here. */
  requestClose(): void {
    if (!this.form.dirty) {
      this.close();
      return;
    }

    this.confirming.set(true);
    this.confirmationService.confirm({
      header: 'Unsaved changes',
      message: 'You have unsaved changes. If you close now, they will be lost.',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Discard',
      rejectLabel: 'Keep editing',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', text: true },
      // Discard sends nothing to the server; the shelf keeps what it already had.
      accept: () => this.close(),
    });
  }

  onSave(): void {
    const item = this.item();
    if (!item || !this.form.dirty) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.shelfService.update(item.id, this.buildRequest()).subscribe({
      next: () => {
        this.saving.set(false);
        this.close();
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.title ?? 'Could not save your changes. Please try again.');
      },
    });
  }

  onRemove(): void {
    const item = this.item();
    if (!item) {
      return;
    }

    this.confirming.set(true);
    this.confirmationService.confirm({
      header: 'Remove from shelf',
      message: `Remove "${item.title}" from your shelf? Your rating, note and dates for it will be lost.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Remove',
      rejectLabel: 'Cancel',
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', text: true },
      accept: () => this.remove(item.id),
    });
  }

  protected onConfirmHidden(): void {
    this.confirming.set(false);
  }

  private remove(userBookId: string): void {
    this.removing.set(true);
    this.errorMessage.set(null);

    this.shelfService.remove(userBookId).subscribe({
      next: () => {
        this.removing.set(false);
        this.removed.emit(userBookId);
        this.close();
      },
      error: (err) => {
        this.removing.set(false);
        this.errorMessage.set(err.error?.title ?? 'Could not remove the book. Please try again.');
      },
    });
  }

  private close(): void {
    this.fillForm(this.item());
    this.visible.set(false);
  }

  /**
   * Only the controls the user actually touched are sent, so a field left alone keeps whatever
   * the server has. Cleared fields carry the agreed sentinels: an empty string for the note and
   * the dates, 0 for the rating.
   */
  private buildRequest(): UpdateShelfItemRequest {
    const controls = this.form.controls;
    const request: UpdateShelfItemRequest = { today: this.today() };

    if (controls.status.dirty) {
      request.status = controls.status.value;
    }

    if (controls.rating.dirty) {
      request.rating = controls.rating.value ?? 0;
    }

    if (controls.note.dirty) {
      request.note = controls.note.value.trim();
    }

    if (controls.startedAt.dirty) {
      request.startedAt = controls.startedAt.value ?? '';
    }

    if (controls.finishedAt.dirty) {
      request.finishedAt = controls.finishedAt.value ?? '';
    }

    if (controls.pageCount.dirty && controls.pageCount.value) {
      request.pageCount = controls.pageCount.value;
    }

    return request;
  }

  /**
   * The reader's calendar day, built from local parts on purpose: `toISOString()` would send the
   * UTC day and stamp yesterday on someone finishing a book late at night.
   */
  private today(): string {
    const now = new Date();
    const pad = (value: number) => String(value).padStart(2, '0');

    return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
  }
}
