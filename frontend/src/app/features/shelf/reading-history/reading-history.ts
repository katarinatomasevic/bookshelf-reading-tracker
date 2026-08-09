import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { ReadingLogEntry } from '../../../core/models/reading-log.model';
import { ReadingLogService } from '../../../core/services/reading-log.service';
import { ShelfItem } from '../../../core/models/shelf.model';
import { formatLocalDate } from '../../../core/utils/local-date';

/**
 * The reading history of one book, as a collapsed section inside the shelf modal.
 *
 * Collapsed by default and fetched only when opened: most visits to the modal are about a rating
 * or a status, and a list of past days would be both noise and a request nobody asked for. The
 * number of entries travels with the shelf entry itself, which is what lets the heading name it
 * while the entries stay unfetched — and lets the parent hide the section entirely for a book
 * with no reading behind it.
 *
 * Editing is inline. A dialog stacked on the shelf modal would be a second layer of "unsaved
 * changes" over a record that is not a draft: an entry is a day that happened, so it is corrected
 * or removed, never discarded.
 */
@Component({
  selector: 'app-reading-history',
  imports: [FormsModule, ButtonModule, InputTextModule, MessageModule],
  templateUrl: './reading-history.html',
  styleUrl: './reading-history.scss',
})
export class ReadingHistory {
  private readonly readingLogService = inject(ReadingLogService);

  readonly userBookId = input.required<string>();
  readonly entryCount = input.required<number>();

  /**
   * Changes whenever a day is logged above this section. It cannot be the entry count: a second
   * entry on the same day raises no count, yet the row already on screen has to show its new
   * total.
   */
  readonly reloadToken = input(0);

  /** Every correction returns the whole shelf entry, because the current page has moved. */
  readonly changed = output<ShelfItem>();

  protected readonly expanded = signal(false);
  protected readonly loading = signal(false);
  protected readonly entries = signal<ReadingLogEntry[]>([]);
  protected readonly errorMessage = signal<string | null>(null);

  /** Which entry is open for editing, and the number currently typed into it. */
  protected readonly editingId = signal<string | null>(null);
  protected editingPages: number | null = null;

  /**
   * The entry awaiting a delete confirmation. Inline rather than a PrimeNG confirm dialog: that
   * would be a third layer stacked over the shelf modal, which already owns one for removing the
   * book itself.
   */
  protected readonly pendingRemoval = signal<ReadingLogEntry | null>(null);

  /** Disables the row being written, so a double click cannot send the same change twice. */
  protected readonly busyId = signal<string | null>(null);

  /** Opened, the list shows five entries; the rest is one click away inside a fixed height. */
  private readonly previewCount = 5;
  protected readonly showingAll = signal(false);

  protected readonly visibleEntries = computed(() =>
    this.showingAll() ? this.entries() : this.entries().slice(0, this.previewCount),
  );

  protected readonly hasMore = computed(() => this.entries().length > this.previewCount);

  constructor() {
    // The shelf page keeps one modal and swaps the book inside it, so this component is reused
    // rather than rebuilt. Without this, the entries loaded for one book stay on screen under the
    // next book's heading — a list that belongs to a different book entirely.
    effect(() => {
      this.userBookId();

      untracked(() => this.resetForNewBook());
    });

    // A logged day makes the loaded list stale. Refetched if the reader is looking at it,
    // discarded if not, so the next opening starts from the server rather than from a cache that
    // was already wrong.
    effect(() => {
      this.reloadToken();

      untracked(() => {
        if (this.entries().length === 0) {
          return;
        }

        if (this.expanded()) {
          this.load();
        } else {
          this.entries.set([]);
        }
      });
    });
  }

  /** Everything here describes one book; none of it may survive a change of book. */
  private resetForNewBook(): void {
    this.entries.set([]);
    this.expanded.set(false);
    this.showingAll.set(false);
    this.editingId.set(null);
    this.editingPages = null;
    this.pendingRemoval.set(null);
    this.errorMessage.set(null);
    this.busyId.set(null);
  }

  protected toggle(): void {
    const opening = !this.expanded();
    this.expanded.set(opening);

    // Fetched on the first opening only. The list is changed from nowhere but this component, so
    // what is already loaded is already current.
    if (opening && this.entries().length === 0) {
      this.load();
    }
  }

  private load(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.readingLogService.getHistory(this.userBookId()).subscribe({
      next: (entries) => {
        this.loading.set(false);
        this.entries.set(entries);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Could not load the reading history. Please try again.');
      },
    });
  }

  protected startEdit(entry: ReadingLogEntry): void {
    this.editingId.set(entry.id);
    this.editingPages = entry.pagesRead;
    this.pendingRemoval.set(null);
    this.errorMessage.set(null);
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.editingPages = null;
  }

  protected saveEdit(entry: ReadingLogEntry): void {
    const pagesRead = this.editingPages;

    if (pagesRead === null || pagesRead <= 0) {
      this.errorMessage.set('Enter a number greater than 0, or delete the entry instead.');
      return;
    }

    if (pagesRead === entry.pagesRead) {
      this.cancelEdit();
      return;
    }

    this.busyId.set(entry.id);
    this.errorMessage.set(null);

    this.readingLogService.update(entry.id, { pagesRead }).subscribe({
      next: (item) => {
        this.busyId.set(null);
        this.cancelEdit();

        // Only this row changes. Because the log stores increments rather than positions, a
        // correction to one day says nothing about what any other day meant — no other entry is
        // affected and there is nothing to re-fetch.
        this.entries.update((current) =>
          current.map((existing) =>
            existing.id === entry.id ? { ...existing, pagesRead } : existing,
          ),
        );

        this.changed.emit(item);
      },
      error: (err) => {
        this.busyId.set(null);
        this.errorMessage.set(
          err.error?.title ?? 'Could not save the correction. Please try again.',
        );
      },
    });
  }

  protected confirmRemove(entry: ReadingLogEntry): void {
    this.cancelEdit();
    this.pendingRemoval.set(entry);
    this.errorMessage.set(null);
  }

  protected cancelRemove(): void {
    this.pendingRemoval.set(null);
  }

  protected remove(entry: ReadingLogEntry): void {
    this.busyId.set(entry.id);
    this.errorMessage.set(null);

    this.readingLogService.remove(entry.id).subscribe({
      next: (item) => {
        this.busyId.set(null);
        this.pendingRemoval.set(null);
        this.entries.update((current) => current.filter((existing) => existing.id !== entry.id));
        this.changed.emit(item);
      },
      error: () => {
        this.busyId.set(null);
        this.errorMessage.set('Could not delete the entry. Please try again.');
      },
    });
  }

  /** Shared with the progress entry above, so both name a day the same way. */
  protected readonly formatDate = formatLocalDate;
}
