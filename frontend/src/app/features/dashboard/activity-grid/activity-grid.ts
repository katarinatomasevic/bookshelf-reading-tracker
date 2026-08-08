import { Component, computed, input } from '@angular/core';
import { Activity } from '../../../core/models/dashboard.model';
import { localDate } from '../../../core/utils/local-date';

interface ActivityCell {
  date: string;
  x: number;
  y: number;
  className: string;
  title: string;
}

interface MonthLabel {
  label: string;
  x: number;
}

/**
 * The activity grid, written by hand as SVG rather than pulled from a charting library: it is
 * seven rows of coloured squares, which is a `<rect>` per day and a colour scale — some thirty
 * lines. The Angular libraries that draw this are either hard to restyle or lag behind the
 * current Angular version, and neither price is worth paying for a grid this simple.
 *
 * 26 weeks, not 52: a year is around 700px wide, which barely fits a laptop and never fits a
 * phone. Half a year stays readable everywhere, and the app is too young for a year of history
 * to be anything but mostly empty.
 */
@Component({
  selector: 'app-activity-grid',
  imports: [],
  templateUrl: './activity-grid.html',
  styleUrl: './activity-grid.scss',
})
export class ActivityGrid {
  readonly activity = input.required<Activity>();

  protected readonly cellSize = 12;
  private readonly step = 15;
  private readonly gutterLeft = 30;
  private readonly gutterTop = 18;
  private readonly rows = 7;

  /** Monday, Wednesday, Friday — enough to orient by without crowding the left edge. */
  protected readonly weekdayLabels = [
    { text: 'Mon', row: 0 },
    { text: 'Wed', row: 2 },
    { text: 'Fri', row: 4 },
  ].map((label) => ({ text: label.text, y: this.gutterTop + label.row * this.step + 10 }));

  /**
   * Room to the right of the last column. Without it the final month label — which starts at the
   * last column and runs past it — is cut off mid-word.
   */
  private readonly gutterRight = 18;

  protected readonly columns = computed(() => Math.ceil(this.activity().days.length / this.rows));

  protected readonly width = computed(
    () =>
      this.gutterLeft + this.columns() * this.step - (this.step - this.cellSize) + this.gutterRight,
  );

  protected readonly height = computed(
    () => this.gutterTop + this.rows * this.step - (this.step - this.cellSize),
  );

  protected readonly cells = computed<ActivityCell[]>(() => {
    const today = localDate();

    return this.activity().days.map((day, index) => {
      // The backend sends the days in grid order, starting on a Monday, so the position is the
      // index itself: a column per week, a row per weekday.
      const column = Math.floor(index / this.rows);
      const row = index % this.rows;
      const future = day.date > today;

      return {
        date: day.date,
        x: this.gutterLeft + column * this.step,
        y: this.gutterTop + row * this.step,
        className: future
          ? 'activity__cell activity__cell--future'
          : `activity__cell activity__cell--${this.level(day.pages)}`,
        title: future
          ? ''
          : `${day.pages === 0 ? 'No reading' : `${day.pages} pages`} on ${this.formatDate(day.date)}`,
      };
    });
  });

  /**
   * What each shade means, shown on the legend swatches. Written from the same thresholds the
   * colours are chosen by, so the two cannot drift apart.
   */
  protected readonly legend = [
    { level: 0, title: 'No reading' },
    { level: 1, title: '1–15 pages' },
    { level: 2, title: '16–40 pages' },
    { level: 3, title: '41–80 pages' },
    { level: 4, title: '81+ pages' },
  ];

  /** A label above the first column of each month, skipped when it would sit on the previous one. */
  protected readonly monthLabels = computed<MonthLabel[]>(() => {
    const days = this.activity().days;
    const labels: MonthLabel[] = [];

    for (let column = 0; column < this.columns(); column++) {
      const day = days[column * this.rows];
      if (!day) {
        continue;
      }

      const month = day.date.slice(0, 7);
      const previous = column === 0 ? null : days[(column - 1) * this.rows]?.date.slice(0, 7);

      if (month === previous) {
        continue;
      }

      const x = this.gutterLeft + column * this.step;
      const last = labels.at(-1);
      if (last && x - last.x < this.step * 3) {
        continue;
      }

      labels.push({ label: this.formatMonth(day.date), x });
    }

    return labels;
  });

  /**
   * The scale from the decisions document, in pages per day. Five steps rather than a continuous
   * gradient, because the grid answers "did I read, and roughly how much" — not "exactly how
   * many pages", which is what the number in the tooltip is for.
   */
  private level(pages: number): number {
    if (pages === 0) return 0;
    if (pages <= 15) return 1;
    if (pages <= 40) return 2;
    if (pages <= 80) return 3;

    return 4;
  }

  /**
   * Built from the parts rather than `new Date('2026-08-03')`, which the browser reads as UTC
   * midnight and can render as the day before.
   */
  private toLocalDate(value: string): Date {
    const [year, month, day] = value.split('-').map(Number);
    return new Date(year, month - 1, day);
  }

  private formatDate(value: string): string {
    return this.toLocalDate(value).toLocaleDateString('en-GB', {
      weekday: 'short',
      day: 'numeric',
      month: 'short',
    });
  }

  private formatMonth(value: string): string {
    return this.toLocalDate(value).toLocaleDateString('en-GB', { month: 'short' });
  }
}
