import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';
import { TabsModule } from 'primeng/tabs';
import { ReadingStatus, ShelfItem, ShelfSort } from '../../core/models/shelf.model';
import { BookCard } from '../../shared/components/book-card/book-card';
import { BookDetailModal } from './book-detail-modal/book-detail-modal';
import { ManualBookDialog } from './manual-book-dialog/manual-book-dialog';
import { ShelfService } from './shelf.service';

@Component({
  selector: 'app-shelf',
  imports: [
    NgTemplateOutlet,
    FormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    ProgressSpinnerModule,
    SelectModule,
    TabsModule,
    BookCard,
    BookDetailModal,
    ManualBookDialog,
  ],
  templateUrl: './shelf.html',
  styleUrl: './shelf.scss',
})
export class Shelf implements OnInit {
  protected readonly shelfService = inject(ShelfService);

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly activeTab = signal<ReadingStatus>(ReadingStatus.WantToRead);
  protected readonly showManualDialog = signal(false);

  protected readonly searchTerm = signal('');
  protected readonly sort = signal<ShelfSort>('added_desc');

  /** The open entry is tracked by id, not by value: after a save the shelf signal holds a new
   *  object, and looking it up keeps the modal showing what the server actually stored. */
  protected readonly selectedId = signal<string | null>(null);
  protected readonly showDetailModal = signal(false);

  protected readonly sortOptions: { label: string; value: ShelfSort }[] = [
    { label: 'Recently added', value: 'added_desc' },
    { label: 'Title', value: 'title' },
    { label: 'Highest rated', value: 'rating_desc' },
    { label: 'Recently finished', value: 'finished_desc' },
  ];

  protected readonly isSearching = computed(() => this.searchTerm().trim().length > 0);

  /** Searching and sorting both run on what is already loaded — the whole shelf arrives in one
   *  request, so neither costs a round trip. */
  private readonly visibleItems = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const items = this.shelfService.items();
    const matches = term ? items.filter((item) => this.haystack(item).includes(term)) : items;

    return this.applySort(matches, this.sort());
  });

  protected readonly wantToRead = computed(() => this.byStatus(ReadingStatus.WantToRead));
  protected readonly reading = computed(() => this.byStatus(ReadingStatus.Reading));
  protected readonly read = computed(() => this.byStatus(ReadingStatus.Read));

  protected readonly isEmpty = computed(
    () => this.shelfService.loaded() && this.shelfService.items().length === 0,
  );

  protected readonly selectedItem = computed(() => {
    const id = this.selectedId();
    return id ? (this.shelfService.items().find((item) => item.id === id) ?? null) : null;
  });

  /** Tabs other than the active one that do have hits, so an empty tab can say where to look. */
  protected readonly hitsInOtherTabs = computed(() => {
    const counts = [
      { label: 'Want to read', count: this.wantToRead().length },
      { label: 'Reading', count: this.reading().length },
      { label: 'Read', count: this.read().length },
    ];

    return counts.filter((tab, index) => index !== this.activeTab() && tab.count > 0);
  });

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

  onSelect(item: ShelfItem): void {
    this.selectedId.set(item.id);
    this.showDetailModal.set(true);
  }

  onRemoved(): void {
    this.selectedId.set(null);
  }

  private byStatus(status: ReadingStatus): ShelfItem[] {
    return this.visibleItems().filter((item) => item.status === status);
  }

  /** Title, author and genres — the same three fields the shelf search promises. */
  private haystack(item: ShelfItem): string {
    return [item.title, item.author ?? '', (item.subjects ?? []).join(' ')].join(' ').toLowerCase();
  }

  /** Mirrors the backend's `?sort=` ordering, including where the empty values land. */
  private applySort(items: ShelfItem[], sort: ShelfSort): ShelfItem[] {
    const byAddedDesc = (a: ShelfItem, b: ShelfItem) => b.addedAt.localeCompare(a.addedAt);

    const sorted = [...items];

    switch (sort) {
      case 'title':
        return sorted.sort((a, b) => a.title.localeCompare(b.title));

      // Unrated and unfinished books go last, which is what "highest rated" and
      // "recently finished" are expected to mean.
      case 'rating_desc':
        return sorted.sort(
          (a, b) => (b.rating ?? 0) - (a.rating ?? 0) || byAddedDesc(a, b),
        );

      case 'finished_desc':
        return sorted.sort(
          (a, b) => (b.finishedAt ?? '').localeCompare(a.finishedAt ?? '') || byAddedDesc(a, b),
        );

      default:
        return sorted.sort(byAddedDesc);
    }
  }
}
