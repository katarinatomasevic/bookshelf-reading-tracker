import { Component, computed, input } from '@angular/core';
import { ProgressBarModule } from 'primeng/progressbar';

/**
 * How far through a book the reader is. Shared by the shelf card and the shelf modal so the one
 * rule that matters lives in a single place: without a page count there is no percentage to
 * show, and a bar drawn anyway would be inventing one.
 */
@Component({
  selector: 'app-reading-progress',
  imports: [ProgressBarModule],
  templateUrl: './reading-progress.html',
  styleUrl: './reading-progress.scss',
})
export class ReadingProgress {
  readonly currentPage = input<number | null>(null);
  readonly pageCount = input<number | null>(null);

  /** Never started counts as page 0 rather than as "unknown": the reader is at the beginning. */
  protected readonly page = computed(() => this.currentPage() ?? 0);

  /**
   * Null when Open Library left no page count. Clamped because a page count filled in later can
   * be lower than pages already logged, and a bar past 100% would look like a rendering bug.
   */
  protected readonly percent = computed(() => {
    const total = this.pageCount();
    if (!total || total <= 0) {
      return null;
    }

    return Math.min(100, Math.round((this.page() / total) * 100));
  });

  /** Separate from {@link percent} because 0% is a real value the template must still draw. */
  protected readonly hasBar = computed(() => this.percent() !== null);

  /** With no page count and no progress there is nothing worth a line of its own. */
  protected readonly hasSomethingToShow = computed(() => this.hasBar() || this.page() > 0);
}
