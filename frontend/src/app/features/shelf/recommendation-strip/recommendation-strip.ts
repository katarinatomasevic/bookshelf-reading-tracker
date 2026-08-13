import { Component, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { Recommendation } from '../../../core/models/recommendation.model';
import { RecommendationCard } from '../../../shared/components/recommendation-card/recommendation-card';
import { RecommendationService } from '../../recommendations/recommendation.service';
import { ShelfService } from '../shelf.service';

/** Shown at once. The slider scrolls rather than the page growing a second grid. */
const STRIP_LIMIT = 10;

/**
 * Fetched beyond what is shown, so a book added from the strip can be replaced without going
 * back to the server for a single card.
 */
const SPARE_COUNT = 5;

/** How long an added book stays visible, marked, before it gives up its place. */
const SWAP_DELAY_MS = 5000;

/** Long enough for the fade to be seen, short enough not to feel like waiting. */
const FADE_MS = 300;

/**
 * "What next" at the foot of the shelf — the only place recommendations appear.
 *
 * <p>There was a `/recommendations` page as well at first, and it was removed: it rendered the
 * same cards from the same call with a different limit, so it was a route that existed because
 * the plan named one, not because it did anything. The shelf is where a reader finishes a book
 * and wonders what to read next, so the whole feature lives here.</p>
 *
 * <p>Three of the four triggers meet in this component: the strip loads when the shelf opens,
 * reloads when a change to the shelf could change what should be recommended, and advances when
 * the reader asks for different books.</p>
 */
@Component({
  selector: 'app-recommendation-strip',
  imports: [ButtonModule, RecommendationCard],
  templateUrl: './recommendation-strip.html',
  styleUrl: './recommendation-strip.scss',
})
export class RecommendationStrip {
  private readonly recommendationService = inject(RecommendationService);
  private readonly shelfService = inject(ShelfService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly items = signal<Recommendation[]>([]);
  protected readonly isColdStart = signal(false);
  protected readonly loading = signal(true);

  /** No message is shown when this fails. The strip is a suggestion at the foot of somebody
   *  else's page; an error banner under their shelf would be louder than the feature is worth. */
  protected readonly failed = signal(false);

  /** Books held back from the visible list, used to fill the gap an added book leaves. */
  private readonly spares = signal<Recommendation[]>([]);

  /** Where the visible window starts. The refresh button moves it; the backend wraps around. */
  private offset = 0;

  protected readonly addedBookIds = signal<ReadonlySet<string>>(new Set());
  protected readonly leavingBookIds = signal<ReadonlySet<string>>(new Set());
  protected readonly pendingAdd = signal<string | null>(null);

  protected readonly skeletons = Array.from({ length: STRIP_LIMIT }, (_, index) => index);

  /** Every pending swap, so none of them fires into a destroyed component. */
  private readonly timers = new Set<ReturnType<typeof setTimeout>>();

  constructor() {
    effect(() => {
      // The dependency. Reading it first, and reading nothing else tracked afterwards, is what
      // keeps this to "a rating or status changed, or a book was removed" — the shelf's own
      // contents are deliberately not a dependency, or every note edit and every page logged
      // would trigger a request.
      this.shelfService.tasteChanged();

      untracked(() => {
        // A changed shelf means a changed list, so paging through the old one is meaningless.
        this.offset = 0;
        this.load();
      });
    });

    this.destroyRef.onDestroy(() => {
      this.timers.forEach((timer) => clearTimeout(timer));
      this.timers.clear();
    });
  }

  /**
   * Asks for the next window rather than the same one again. This is the whole reason the button
   * is defensible: the list is a deterministic function of the shelf, so a button that re-fetched
   * it would redraw an identical page and look broken.
   */
  protected onRefresh(): void {
    this.offset += STRIP_LIMIT;
    this.load();
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
        this.scheduleSwap(recommendation.bookId);
      },
      error: () => this.pendingAdd.set(null),
    });
  }

  /**
   * Lets an added book sit there, ticked, before quietly giving up its place to another.
   *
   * <p>The card does not vanish the instant it is clicked, because the reader is still looking at
   * it and a card disappearing under the cursor reads as a mistake. It also does not stay
   * forever: it is on the shelf now, and a list of what to read next should not be advertising
   * books already on it.</p>
   *
   * <p>Each book carries its own pair of timers, so adding three books in quick succession gives
   * three independent swaps rather than one queue.</p>
   */
  private scheduleSwap(bookId: string): void {
    this.defer(() => {
      this.leavingBookIds.update((current) => new Set(current).add(bookId));

      // Replaced only after the fade has played, so the swap is seen rather than noticed.
      this.defer(() => this.replace(bookId), FADE_MS);
    }, SWAP_DELAY_MS);
  }

  private replace(bookId: string): void {
    const replacement = this.spares()[0] ?? null;
    if (replacement) {
      this.spares.update((current) => current.slice(1));
    }

    this.items.update((current) => {
      const index = current.findIndex((item) => item.bookId === bookId);
      if (index === -1) {
        return current;
      }

      const updated = [...current];
      // With nothing held back the card simply leaves; a strip of nine is better than one
      // advertising a book the reader has just shelved.
      if (replacement) {
        updated[index] = replacement;
      } else {
        updated.splice(index, 1);
      }

      return updated;
    });

    this.forget(this.addedBookIds, bookId);
    this.forget(this.leavingBookIds, bookId);
  }

  private load(): void {
    this.loading.set(true);
    this.failed.set(false);

    // One request covers both the strip and its spares.
    this.recommendationService.getRecommendations(STRIP_LIMIT + SPARE_COUNT, this.offset).subscribe({
      next: (recommendations) => {
        this.items.set(recommendations.items.slice(0, STRIP_LIMIT));
        this.spares.set(recommendations.items.slice(STRIP_LIMIT));
        this.isColdStart.set(recommendations.isColdStart);

        // Marks belong to the list they were made against.
        this.addedBookIds.set(new Set());
        this.leavingBookIds.set(new Set());
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.failed.set(true);
      },
    });
  }

  private defer(action: () => void, delay: number): void {
    const timer = setTimeout(() => {
      this.timers.delete(timer);
      action();
    }, delay);

    this.timers.add(timer);
  }

  private forget(target: typeof this.addedBookIds, bookId: string): void {
    target.update((current) => {
      const updated = new Set(current);
      updated.delete(bookId);
      return updated;
    });
  }
}
