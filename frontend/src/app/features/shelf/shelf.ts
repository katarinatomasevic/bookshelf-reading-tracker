import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TabsModule } from 'primeng/tabs';
import { ReadingStatus, ShelfItem } from '../../core/models/shelf.model';
import { BookCard } from '../../shared/components/book-card/book-card';
import { ManualBookDialog } from './manual-book-dialog/manual-book-dialog';
import { ShelfService } from './shelf.service';

@Component({
  selector: 'app-shelf',
  imports: [RouterLink, ButtonModule, ProgressSpinnerModule, TabsModule, BookCard, ManualBookDialog],
  templateUrl: './shelf.html',
  styleUrl: './shelf.scss',
})
export class Shelf implements OnInit {
  protected readonly shelfService = inject(ShelfService);
  private readonly router = inject(Router);

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly activeTab = signal<ReadingStatus>(ReadingStatus.WantToRead);
  protected readonly showManualDialog = signal(false);

  /** The shelf arrives in one request and is split here; tab counts then cost nothing,
   *  and the next phase's in-tab search can run entirely on what is already loaded. */
  protected readonly wantToRead = computed(() => this.byStatus(ReadingStatus.WantToRead));
  protected readonly reading = computed(() => this.byStatus(ReadingStatus.Reading));
  protected readonly read = computed(() => this.byStatus(ReadingStatus.Read));

  protected readonly isEmpty = computed(
    () => this.shelfService.loaded() && this.shelfService.items().length === 0,
  );

  ngOnInit(): void {
    this.loading.set(true);
    this.error.set(null);

    this.shelfService.getShelf().subscribe({
      next: () => this.loading.set(false),
      error: () => {
        this.loading.set(false);
        this.error.set('Could not load your shelf. Please try again.');
      },
    });
  }

  /** The shelf signal already holds the new book; this only moves the user to where it landed. */
  onManualCreated(item: ShelfItem): void {
    this.activeTab.set(item.status);
  }

  /** Manually added books have no Open Library key, so they are opened by our own id. */
  onSelect(item: ShelfItem): void {
    this.router.navigate(['/books', item.openLibraryId ?? item.bookId]);
  }

  private byStatus(status: ReadingStatus): ShelfItem[] {
    return this.shelfService.items().filter((item) => item.status === status);
  }
}
