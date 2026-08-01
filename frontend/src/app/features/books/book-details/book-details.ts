import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { BookService } from '../book.service';
import { BookDetails } from '../../../core/models/book.model';

@Component({
  selector: 'app-book-details',
  imports: [RouterLink, ProgressSpinnerModule],
  templateUrl: './book-details.html',
  styleUrl: './book-details.scss',
})
export class BookDetailsPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly bookService = inject(BookService);

  protected readonly book = signal<BookDetails | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

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
}
