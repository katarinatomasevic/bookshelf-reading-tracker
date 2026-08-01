import { Component, computed, input, output } from '@angular/core';
import { BookCardData } from '../../../core/models/book.model';
import { AddToShelfButton } from '../add-to-shelf-button/add-to-shelf-button';

@Component({
  selector: 'app-book-card',
  imports: [AddToShelfButton],
  templateUrl: './book-card.html',
  styleUrl: './book-card.scss',
})
export class BookCard {
  /** Deliberately the narrow card shape: search results and shelf items both fit it. */
  readonly book = input.required<BookCardData>();
  readonly showAddButton = input(false);
  readonly isOnShelf = input(false);
  readonly addPending = input(false);

  readonly select = output<void>();
  readonly addToShelf = output<void>();

  protected readonly coverUrl = computed(() => {
    const coverId = this.book().coverId;
    return coverId ? `https://covers.openlibrary.org/b/id/${coverId}-M.jpg` : null;
  });

  onSelect(): void {
    this.select.emit();
  }
}
