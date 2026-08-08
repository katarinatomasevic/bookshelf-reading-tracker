import {
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ChartModule } from 'primeng/chart';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';
import { Activity, DashboardPeriod, DashboardSummary } from '../../core/models/dashboard.model';
import { LogProgressResponse } from '../../core/models/reading-log.model';
import { ReadingStatus } from '../../core/models/shelf.model';
import { localDate } from '../../core/utils/local-date';
import { ShelfService } from '../shelf/shelf.service';
import { ActivityGrid } from './activity-grid/activity-grid';
import { CurrentlyReadingCard } from './currently-reading-card/currently-reading-card';
import { DashboardService } from './dashboard.service';

/**
 * The reading dashboard. The order of the sections is deliberate: what the reader acts on first
 * (logging today's pages, then the streak that depends on it), then the numbers, then the charts.
 *
 * The "currently reading" strip is at the top for the reason the whole page exists: a streak is
 * only kept if logging is effortless. If the dashboard showed a six-day streak but the seventh
 * day meant going to the shelf, finding the book and opening a dialog, the streak would break on
 * friction alone — and the dashboard is the one place where the reader sees what logging is for.
 *
 * The page reuses ShelfService rather than fetching "reading" books through a service of its own:
 * the shelf is one domain and deserves one HTTP service, and the whole shelf is needed anyway to
 * know whether it is empty.
 */
@Component({
  selector: 'app-dashboard',
  imports: [
    FormsModule,
    RouterLink,
    ButtonModule,
    ChartModule,
    ConfirmDialogModule,
    ProgressSpinnerModule,
    SelectModule,
    ActivityGrid,
    CurrentlyReadingCard,
  ],
  providers: [ConfirmationService],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit, AfterViewInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly shelfService = inject(ShelfService);

  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly activity = signal<Activity | null>(null);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  /**
   * Everything the page needs, including the shelf: without it the empty state could not be told
   * apart from a shelf that simply has not arrived yet, and the page would flash the wrong one.
   */
  protected readonly ready = computed(
    () => this.summary() !== null && this.activity() !== null && this.shelfService.loaded(),
  );

  protected readonly period = signal<DashboardPeriod>('this_year');

  protected readonly periodOptions: { label: string; value: DashboardPeriod }[] = [
    // A rolling window rather than a calendar month, matching the 30 days the reading log accepts.
    { label: 'Last 30 days', value: 'last_30_days' },
    { label: 'This year', value: 'this_year' },
    { label: 'Last year', value: 'last_year' },
    { label: 'All time', value: 'all_time' },
  ];

  protected readonly currentlyReading = computed(() =>
    this.shelfService.items().filter((item) => item.status === ReadingStatus.Reading),
  );

  /**
   * The empty state is about an empty shelf, not about empty numbers. A reader with books but no
   * logs yet should still see the page — zeros there are honest, and the charts say so themselves.
   */
  protected readonly shelfEmpty = computed(
    () => this.shelfService.loaded() && this.shelfService.items().length === 0,
  );

  /**
   * A hidden element whose colours are set from the theme's tokens. The charts draw into a
   * `<canvas>`, which cannot use CSS variables, so Chart.js needs concrete colours — and reading
   * the custom property directly is not enough, because the theme defines them through
   * `light-dark()` and the raw value would come back as that function rather than a colour.
   * Reading the *computed* style of a real element resolves it, in whichever theme is active.
   */
  private readonly themeProbe = viewChild.required<ElementRef<HTMLElement>>('themeProbe');

  /** Read from the theme so the charts use the same palette as the rest of the app. */
  private readonly theme = signal<{ primary: string; muted: string; border: string } | null>(null);

  /** Long enough for a real genre, short enough not to eat the plot area. */
  private readonly maxGenreLabelLength = 28;

  protected readonly hasBooksFinishedData = computed(
    () => (this.summary()?.booksFinished ?? []).some((bucket) => bucket.count > 0),
  );

  /** Says what a bar is, so "All time" grouped by year cannot be mistaken for months. */
  protected readonly booksChartTitle = computed(() =>
    this.summary()?.booksFinishedGranularity === 'year' ? 'Books per year' : 'Books per month',
  );

  protected readonly booksChartData = computed(() => {
    const buckets = this.summary()?.booksFinished ?? [];
    const theme = this.theme();

    // Two-digit years only when the range actually spans more than one, so a single year of bars
    // is not repeating the same suffix twelve times.
    const spansYears = new Set(buckets.map((bucket) => bucket.year)).size > 1;

    return {
      labels: buckets.map((bucket) => this.bucketLabel(bucket.year, bucket.month, spansYears)),
      datasets: [
        {
          label: 'Books finished',
          data: buckets.map((bucket) => bucket.count),
          backgroundColor: theme?.primary,
          borderRadius: 4,
        },
      ],
    };
  });

  protected readonly genresChartData = computed(() => {
    const genres = this.summary()?.topGenres ?? [];
    const theme = this.theme();

    return {
      // Truncated, because Open Library subjects run to things like "Canadian fiction (fictional
      // works by one author)" and the axis would otherwise cut them off with no way to read them.
      // The tooltip carries the full name.
      labels: genres.map((genre) => this.truncate(genre.subject)),
      datasets: [
        {
          label: 'Books',
          data: genres.map((genre) => genre.count),
          backgroundColor: theme?.primary,
          borderRadius: 4,
        },
      ],
    };
  });

  protected readonly booksChartOptions = computed(() => this.chartOptions('x'));

  /** Horizontal bars: subject names are long enough that upright labels would be unreadable. */
  protected readonly genresChartOptions = computed(() =>
    this.chartOptions(
      'y',
      (this.summary()?.topGenres ?? []).map((genre) => genre.subject),
    ),
  );

  constructor() {
    // Everything else on the page follows the theme on its own, through CSS variables; the charts
    // are painted onto a canvas, so they have to be told when the browser switches.
    const darkMode = window.matchMedia('(prefers-color-scheme: dark)');
    const onThemeChange = () => this.readTheme();

    darkMode.addEventListener('change', onThemeChange);
    this.destroyRef.onDestroy(() => darkMode.removeEventListener('change', onThemeChange));
  }

  ngOnInit(): void {
    // The shelf may already be in memory from a visit to /shelf; the signal is the same one.
    if (!this.shelfService.loaded()) {
      this.shelfService.getShelf().subscribe({ error: () => this.failed() });
    }

    this.load();
  }

  /** The probe only has resolved colours once it is in the document. */
  ngAfterViewInit(): void {
    this.readTheme();
  }

  protected onPeriodChange(period: DashboardPeriod): void {
    this.period.set(period);
    this.loadSummary();
  }

  /**
   * Progress logged from the strip. The shelf entry comes back with the response, and both reads
   * are repeated rather than patched by hand: the streak and the grid are the whole point of
   * logging from here, and they are cheap to recompute compared to getting them subtly wrong.
   */
  protected onLogged(response: LogProgressResponse): void {
    this.shelfService.applyItem(response.item);
    this.load();

    if (!response.bookCompleted) {
      return;
    }

    this.confirmationService.confirm({
      header: 'Finished?',
      message: 'You reached the end! Mark this book as read?',
      icon: 'pi pi-check-circle',
      acceptLabel: 'Yes',
      rejectLabel: 'Not yet',
      rejectButtonProps: { severity: 'secondary', text: true },
      accept: () => this.markAsRead(response.item.id),
    });
  }

  /**
   * A book can also sit at 100% from an earlier session — someone who answered "Not yet" then has
   * no other way forward here, because the log refuses to go past the last page. The card offers
   * the same action, and it goes through the same PATCH.
   */
  protected onMarkAsRead(userBookId: string): void {
    this.markAsRead(userBookId);
  }

  private markAsRead(userBookId: string): void {
    this.shelfService
      .update(userBookId, { status: ReadingStatus.Read, today: localDate() })
      .subscribe({
        // Finishing a book changes the books-read card and the monthly chart, so the summary is
        // read again; the book also leaves the strip on its own, through the shelf signal.
        next: () => this.loadSummary(),
        error: () => this.error.set('Could not update the book. Please try again.'),
      });
  }

  private load(): void {
    this.loadSummary();

    this.dashboardService.getActivity().subscribe({
      next: (activity) => this.activity.set(activity),
      error: () => this.failed(),
    });
  }

  private loadSummary(): void {
    this.dashboardService.getDashboard(this.period()).subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.loading.set(false);
        this.error.set(null);
      },
      error: () => this.failed(),
    });
  }

  private failed(): void {
    this.loading.set(false);
    this.error.set('Could not load your dashboard. Please try again.');
  }

  protected dayLabel(days: number): string {
    return days === 1 ? 'day' : 'days';
  }

  private bucketLabel(year: number, month: number | null, withYear: boolean): string {
    // A yearly bucket has no month, and its label is simply the year.
    if (month === null) {
      return String(year);
    }

    const label = new Date(year, month - 1, 1).toLocaleDateString('en-GB', { month: 'short' });

    return withYear ? `${label} ${String(year).slice(2)}` : label;
  }

  private truncate(value: string): string {
    return value.length > this.maxGenreLabelLength
      ? `${value.slice(0, this.maxGenreLabelLength - 1).trimEnd()}…`
      : value;
  }

  /**
   * Takes the colours the theme resolved on the probe element, rather than inventing a second
   * palette beside the first. Reading it again on a theme switch is what keeps the charts legible
   * in dark mode.
   */
  private readTheme(): void {
    const styles = getComputedStyle(this.themeProbe().nativeElement);

    this.theme.set({
      primary: styles.backgroundColor,
      muted: styles.color,
      border: styles.borderTopColor,
    });
  }

  /**
   * @param fullLabels the untruncated labels, shown in the tooltip; empty when nothing was cut.
   */
  private chartOptions(valueAxis: 'x' | 'y', fullLabels: string[] = []) {
    const theme = this.theme();
    const ticks = { color: theme?.muted, precision: 0 };
    const grid = { color: theme?.border };

    // Left alone when nothing was truncated, so the month chart keeps Chart.js's own title.
    const tooltip =
      fullLabels.length > 0
        ? {
            callbacks: {
              title: (items: { dataIndex: number }[]) => fullLabels[items[0]?.dataIndex] ?? '',
            },
          }
        : {};

    return {
      indexAxis: valueAxis === 'y' ? ('y' as const) : ('x' as const),
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        // One dataset with a name in the heading above it: a legend would repeat the title.
        legend: { display: false },
        tooltip,
      },
      scales: {
        x: {
          ticks,
          grid: valueAxis === 'y' ? grid : { display: false },
          // Books are whole things; a "1.5 books" gridline would be nonsense.
          beginAtZero: true,
        },
        y: {
          ticks,
          grid: valueAxis === 'y' ? { display: false } : grid,
          beginAtZero: true,
        },
      },
    };
  }
}
