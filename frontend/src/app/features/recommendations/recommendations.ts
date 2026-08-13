import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Recommendation } from '../../core/models/recommendation.model';
import { RecommendationCard } from '../../shared/components/recommendation-card/recommendation-card';
import { ShelfService } from '../shelf/shelf.service';
import { RecommendationService } from './recommendation.service';

/** How many the page asks for; the strip at the foot of the shelf asks for five. */
const PAGE_LIMIT = 10;

@Component({
  selector: 'app-recommendations',
  imports: [RecommendationCard],
  templateUrl: './recommendations.html',
  styleUrl: './recommendations.scss',
})
export class Recommendations implements OnInit {
  private readonly recommendationService = inject(RecommendationService);
  private readonly shelfService = inject(ShelfService);
  private readonly router = inject(Router);

  protected readonly items = signal<Recommendation[]>([]);
  protected readonly isColdStart = signal(false);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  /** Books added from this page. They stay in the list — the server only excludes what was on
   *  the shelf when the list was built — so the card is marked instead of disappearing under
   *  the reader's cursor. */
  protected readonly addedBookIds = signal<ReadonlySet<string>>(new Set());
  protected readonly pendingAdd = signal<string | null>(null);
  protected readonly addError = signal<string | null>(null);

  /** Placeholder cards while the request is in flight, so the page has its final shape from the
   *  first paint instead of jumping when the books arrive. */
  protected readonly skeletons = Array.from({ length: PAGE_LIMIT }, (_, index) => index);

  ngOnInit(): void {
    // Opening the page is itself the trigger. There is no refresh button: the same shelf always
    // produces the same list, so a button would redraw an identical page and look broken.
    this.load();
  }

  protected onSelect(recommendation: Recommendation): void {
    this.router.navigate(['/books', recommendation.bookId]);
  }

  protected onAddToShelf(recommendation: Recommendation): void {
    this.pendingAdd.set(recommendation.bookId);
    this.addError.set(null);

    // bookId, not openLibraryId: a recommendation is by definition already a row in our
    // database, so this resolves in a single lookup and works for manually added books too.
    this.shelfService.addToShelf({ bookId: recommendation.bookId }).subscribe({
      next: () => {
        this.pendingAdd.set(null);
        this.addedBookIds.update((current) => new Set(current).add(recommendation.bookId));
      },
      error: () => {
        this.pendingAdd.set(null);
        this.addError.set('Could not add the book to your shelf. Please try again.');
      },
    });
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.recommendationService.getRecommendations(PAGE_LIMIT).subscribe({
      next: (recommendations) => {
        this.items.set(recommendations.items);
        this.isColdStart.set(recommendations.isColdStart);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Could not load recommendations. Please try again.');
      },
    });
  }
}
