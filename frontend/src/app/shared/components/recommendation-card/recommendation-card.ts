import { Component, computed, input, output } from '@angular/core';
import { Recommendation } from '../../../core/models/recommendation.model';
import { BookCard } from '../book-card/book-card';

/**
 * A recommended book: the shared book card, plus the one line that says why it is being
 * recommended.
 *
 * <p>Written as a wrapper rather than as another set of inputs on {@link BookCard} because the
 * attribution is not a property of a book — the same book is "because you liked Dune" to one
 * reader and "Popular" to another. Keeping it out here also means the card stays the single
 * shape that search results, shelf items and recommendations all satisfy.</p>
 */
@Component({
  selector: 'app-recommendation-card',
  imports: [BookCard],
  templateUrl: './recommendation-card.html',
  styleUrl: './recommendation-card.scss',
})
export class RecommendationCard {
  readonly recommendation = input.required<Recommendation>();
  readonly addPending = input(false);
  readonly isOnShelf = input(false);

  readonly select = output<void>();
  readonly addToShelf = output<void>();

  /**
   * Naming the source book is the whole value of the attribution, so the label carries the title
   * rather than a vague "based on your taste". Popular books say so plainly instead of borrowing
   * a phrase that would imply a personalisation that has not happened yet.
   */
  protected readonly reason = computed(() => {
    const basedOn = this.recommendation().basedOn;
    return basedOn ? `Because you liked ${basedOn.title}` : 'Popular';
  });

  protected readonly isPopular = computed(() => this.recommendation().basedOn === null);
}
