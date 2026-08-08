import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { LogProgressResponse } from '../../../core/models/reading-log.model';
import { ShelfItem } from '../../../core/models/shelf.model';
import { ProgressEntry } from '../../../shared/components/progress-entry/progress-entry';
import { ReadingProgress } from '../../../shared/components/reading-progress/reading-progress';

/**
 * One book of the "currently reading" strip: a thumbnail, where the reader is, and the field to
 * move it. Compact on purpose — the strip is a row of these, and its job is to make logging
 * today's pages the first thing on the page rather than something to go looking for.
 *
 * Both the bar and the entry field are the shared components from the shelf modal, so the rules
 * about page counts and about what "to page" means are written once.
 */
@Component({
  selector: 'app-currently-reading-card',
  imports: [RouterLink, ButtonModule, ProgressEntry, ReadingProgress],
  templateUrl: './currently-reading-card.html',
  styleUrl: './currently-reading-card.scss',
})
export class CurrentlyReadingCard {
  readonly item = input.required<ShelfItem>();

  readonly logged = output<LogProgressResponse>();
  readonly markAsRead = output<string>();

  protected readonly coverUrl = computed(() => {
    const coverId = this.item().coverId;
    return coverId ? `https://covers.openlibrary.org/b/id/${coverId}-S.jpg` : null;
  });

  /**
   * At the last page and still on the "reading" shelf. It happens whenever the reader answered
   * "Not yet" to the offer that follows the final entry — and from then on the card is a dead
   * end, because the log will not accept a page past the end of the book.
   *
   * Only ever true when the page count is known: without one there is no end to have reached,
   * which is also why such a book can only be finished by changing its status by hand.
   */
  protected readonly isComplete = computed(() => {
    const { currentPage, pageCount } = this.item();

    return pageCount !== null && currentPage !== null && currentPage >= pageCount;
  });
}
