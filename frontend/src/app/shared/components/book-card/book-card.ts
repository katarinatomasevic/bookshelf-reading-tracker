import { Component, computed, input, output } from '@angular/core';
import { BookCardData } from '../../../core/models/book.model';
import { AddToShelfButton } from '../add-to-shelf-button/add-to-shelf-button';
import { ReadingProgress } from '../reading-progress/reading-progress';

@Component({
  selector: 'app-book-card',
  imports: [AddToShelfButton, ReadingProgress],
  templateUrl: './book-card.html',
  styleUrl: './book-card.scss',
})
export class BookCard {
  /** Deliberately the narrow card shape: search results and shelf items both fit it. */
  readonly book = input.required<BookCardData>();
  readonly showAddButton = input(false);
  readonly isOnShelf = input(false);
  readonly addPending = input(false);

  /**
   * Kept off {@link BookCardData} on purpose: only shelf items can carry a rating, and search
   * results have nothing to put here.
   */
  readonly rating = input<number | null>(null);

  /**
   * Progress is opt-in rather than inferred from the numbers: every book has a page count, but
   * only a book actually being read has somewhere to be in it. The shelf passes this in the
   * "Reading" tab alone.
   */
  readonly showProgress = input(false);
  readonly currentPage = input<number | null>(null);
  readonly pageCount = input<number | null>(null);

  readonly select = output<void>();
  readonly addToShelf = output<void>();

  protected readonly coverUrl = computed(() => {
    const coverId = this.book().coverId;
    return coverId ? `https://covers.openlibrary.org/b/id/${coverId}-M.jpg` : null;
  });

  /** Five slots so the filled ones read as a rating rather than as a count of icons. */
  protected readonly stars = computed(() => {
    const rating = this.rating();
    return rating ? [1, 2, 3, 4, 5].map((star) => star <= rating) : null;
  });

  onSelect(): void {
    this.select.emit();
  }
}
