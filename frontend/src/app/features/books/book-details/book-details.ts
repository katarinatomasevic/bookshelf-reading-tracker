import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { BookService } from '../book.service';
import { BookDetails } from '../../../core/models/book.model';
import { AddToShelfButton } from '../../../shared/components/add-to-shelf-button/add-to-shelf-button';
import { ShelfService } from '../../shelf/shelf.service';

@Component({
  selector: 'app-book-details',
  imports: [RouterLink, ProgressSpinnerModule, AddToShelfButton],
  templateUrl: './book-details.html',
  styleUrl: './book-details.scss',
})
export class BookDetailsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly bookService = inject(BookService);
  private readonly shelfService = inject(ShelfService);

  protected readonly book = signal<BookDetails | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly addPending = signal(false);
  protected readonly addError = signal<string | null>(null);

  protected readonly coverUrl = computed(() => {
    const coverId = this.book()?.coverId;
    return coverId ? `https://covers.openlibrary.org/b/id/${coverId}-L.jpg` : null;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Book not found.');
      this.loading.set(false);
      return;
    }

    this.bookService.getById(id).subscribe({
      next: (details) => {
        this.book.set(details);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('This book could not be found.');
        this.loading.set(false);
      },
    });
  }

  /** A book we already store is identified by our own id; one that lives only on Open
   *  Library is identified by its work key, and has no page count to pass along. */
  onAddToShelf(details: BookDetails): void {
    this.addPending.set(true);
    this.addError.set(null);

    const request = details.id
      ? { bookId: details.id }
      : {
          openLibraryId: details.openLibraryId!,
          pageCount: details.pageCount,
          isbn: details.isbn,
        };

    this.shelfService.addToShelf(request).subscribe({
      next: (item) => {
        this.addPending.set(false);
        this.book.update((current) => (current ? { ...current, id: item.bookId, isOnShelf: true } : current));

        if (details.openLibraryId) {
          // Search results may still be on screen behind this page.
          this.bookService.markOnShelf(details.openLibraryId);
        }
      },
      error: () => {
        this.addPending.set(false);
        this.addError.set('Could not add the book to your shelf. Please try again.');
      },
    });
  }
}
