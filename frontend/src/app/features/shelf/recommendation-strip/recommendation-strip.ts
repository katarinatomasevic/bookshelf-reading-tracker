import { Component, effect, inject, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Recommendation } from '../../../core/models/recommendation.model';
import { RecommendationCard } from '../../../shared/components/recommendation-card/recommendation-card';
import { RecommendationService } from '../../recommendations/recommendation.service';
import { ShelfService } from '../shelf.service';

/** Five: enough to be worth scrolling to, few enough not to compete with the shelf above it. */
const STRIP_LIMIT = 5;

/**
 * "What next" at the foot of the shelf — the place a reader lands after finishing a book.
 *
 * <p>Two of the feature's three triggers meet here: the strip loads when the shelf page opens,
 * and reloads whenever a change to the shelf could change what should be recommended. Neither is
 * a button. The third trigger is opening the recommendations page itself.</p>
 */
@Component({
  selector: 'app-recommendation-strip',
  imports: [RouterLink, RecommendationCard],
  templateUrl: './recommendation-strip.html',
  styleUrl: './recommendation-strip.scss',
})
export class RecommendationStrip {
  private readonly recommendationService = inject(RecommendationService);
  private readonly shelfService = inject(ShelfService);
  private readonly router = inject(Router);

  protected readonly items = signal<Recommendation[]>([]);
  protected readonly isColdStart = signal(false);
  protected readonly loading = signal(true);

  /** No message is shown when this fails. The strip is a suggestion at the bottom of somebody
   *  else's page; an error banner under their shelf would be louder than the feature is worth. */
  protected readonly failed = signal(false);

  protected readonly addedBookIds = signal<ReadonlySet<string>>(new Set());
  protected readonly pendingAdd = signal<string | null>(null);

  protected readonly skeletons = Array.from({ length: STRIP_LIMIT }, (_, index) => index);

  constructor() {
    effect(() => {
      // The dependency. Reading it first, and reading nothing else tracked afterwards, is what
      // keeps this to "a rating or status changed, or a book was removed" — the shelf's own
      // contents are deliberately not a dependency, or every note edit and every page logged
      // would trigger a request.
      this.shelfService.tasteChanged();

      untracked(() => this.load());
    });
  }

  protected onSelect(recommendation: Recommendation): void {
    this.router.navigate(['/books', recommendation.bookId]);
  }

  protected onAddToShelf(recommendation: Recommendation): void {
    this.pendingAdd.set(recommendation.bookId);

    this.shelfService.addToShelf({ bookId: recommendation.bookId }).subscribe({
      next: () => {
        this.pendingAdd.set(null);
        this.addedBookIds.update((current) => new Set(current).add(recommendation.bookId));
      },
      error: () => this.pendingAdd.set(null),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.failed.set(false);

    this.recommendationService.getRecommendations(STRIP_LIMIT).subscribe({
      next: (recommendations) => {
        this.items.set(recommendations.items);
        this.isColdStart.set(recommendations.isColdStart);

        // Books added straight from the strip are no longer candidates, so a refetch would drop
        // them anyway; clearing here keeps the marks from outliving the list they belong to.
        this.addedBookIds.set(new Set());
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.failed.set(true);
      },
    });
  }
}
