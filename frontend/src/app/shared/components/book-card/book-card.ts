import { Component, computed, input, output } from '@angular/core';
import { BookSearchResult } from '../../../core/models/book.model';

@Component({
  selector: 'app-book-card',
  imports: [],
  templateUrl: './book-card.html',
  styleUrl: './book-card.scss',
})
export class BookCard {
  readonly book = input.required<BookSearchResult>();
  readonly select = output<string>();

  protected readonly coverUrl = computed(() => {
    const coverId = this.book().coverId;
    return coverId ? `https://covers.openlibrary.org/b/id/${coverId}-M.jpg` : null;
  });

  onSelect(): void {
    this.select.emit(this.book().openLibraryId);
  }
}
