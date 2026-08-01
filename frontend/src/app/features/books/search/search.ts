import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { BookService } from '../book.service';
import { BookCard } from '../../../shared/components/book-card/book-card';
import { ShelfService } from '../../shelf/shelf.service';
import { BookSearchResult } from '../../../core/models/book.model';

@Component({
  selector: 'app-search',
  imports: [FormsModule, InputTextModule, ButtonModule, ProgressSpinnerModule, BookCard],
  templateUrl: './search.html',
  styleUrl: './search.scss',
})
export class Search {
  protected readonly bookService = inject(BookService);
  private readonly shelfService = inject(ShelfService);
  private readonly router = inject(Router);

  protected queryInput = this.bookService.lastQuery();

  protected readonly loading = signal(false);
  protected readonly loadingMore = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Open Library id of the book currently being added, so only its own button spins. */
  protected readonly pendingAdd = signal<string | null>(null);
  protected readonly addError = signal<string | null>(null);

  constructor() {
    // Logging out clears the shared search state while this page may still be open;
    // the text box is component-local, so it has to follow. Typing doesn't touch any
    // signal, so this only runs when the search state itself changes.
    effect(() => {
      if (!this.bookService.hasSearched()) {
        this.queryInput = '';
      }
    });
  }

  onSearch(): void {
    const query = this.queryInput.trim();
    if (!query) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.bookService.search(query).subscribe({
      next: () => this.loading.set(false),
      error: () => {
        this.loading.set(false);
        this.error.set('Search failed. Please try again.');
      },
    });
  }

  onLoadMore(): void {
    this.loadingMore.set(true);
    this.error.set(null);

    this.bookService.loadMore().subscribe({
      next: () => this.loadingMore.set(false),
      error: () => {
        this.loadingMore.set(false);
        this.error.set('Could not load more results. Please try again.');
      },
    });
  }

  onSelectBook(openLibraryId: string): void {
    this.router.navigate(['/books', openLibraryId]);
  }

  /** Page count and ISBN travel with the request: the details call the backend makes when it
   *  first stores a book returns neither, and later phases need the page count. */
  onAddToShelf(book: BookSearchResult): void {
    this.pendingAdd.set(book.openLibraryId);
    this.addError.set(null);

    this.shelfService
      .addToShelf({
        openLibraryId: book.openLibraryId,
        pageCount: book.pageCount,
        isbn: book.isbn,
      })
      .subscribe({
        next: () => {
          this.pendingAdd.set(null);
          this.bookService.markOnShelf(book.openLibraryId);
        },
        error: () => {
          this.pendingAdd.set(null);
          this.addError.set('Could not add the book to your shelf. Please try again.');
        },
      });
  }
}
