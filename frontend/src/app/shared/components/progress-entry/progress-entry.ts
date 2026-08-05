import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectButtonModule } from 'primeng/selectbutton';
import { LogProgressRequest, LogProgressResponse } from '../../../core/models/reading-log.model';
import { ReadingLogService } from '../../../core/services/reading-log.service';

type EntryMode = 'pagesRead' | 'toPage';

/**
 * Logging a day's reading. Shared on purpose: the shelf modal uses it now and the dashboard's
 * "currently reading" strip will use the same one, because a reader who has to hunt for the
 * field is a reader whose streak breaks.
 *
 * It owns its own small form and posts on its own button, separately from whatever form
 * surrounds it: this writes a ReadingLog, a different record from a shelf entry's own fields,
 * and one that cannot be "discarded" afterwards the way an unsaved rating can. The parent is
 * told through {@link logged} and decides what to refresh.
 */
@Component({
  selector: 'app-progress-entry',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    MessageModule,
    SelectButtonModule,
  ],
  templateUrl: './progress-entry.html',
  styleUrl: './progress-entry.scss',
})
export class ProgressEntry {
  private readonly fb = inject(FormBuilder);
  private readonly readingLogService = inject(ReadingLogService);

  readonly userBookId = input.required<string>();
  readonly currentPage = input<number | null>(null);
  readonly pageCount = input<number | null>(null);

  readonly logged = output<LogProgressResponse>();

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  /** Mirrors the backend window; the date input refuses what the API would reject anyway. */
  private readonly maxBackdatedDays = 30;

  protected readonly maxDate = this.today();
  protected readonly minDate = this.dateDaysAgo(this.maxBackdatedDays);

  protected readonly form = this.fb.nonNullable.group({
    mode: ['pagesRead' as EntryMode],
    amount: [null as number | null, [Validators.required, Validators.min(1)]],
    date: [this.maxDate, Validators.required],
  });

  /** Signals rather than the controls themselves, so the rules below can be computed. */
  protected readonly mode = signal<EntryMode>('pagesRead');
  protected readonly date = signal(this.maxDate);

  private readonly amountValue = toSignal(this.form.controls.amount.valueChanges, {
    initialValue: null as number | null,
  });

  protected readonly isToday = computed(() => this.date() === this.maxDate);

  /**
   * "To page" is off the table for an earlier day. The reader means "on that day I got to page
   * 25", but the only position the system knows is the one they are at now — the log stores
   * increments, not positions, so an earlier date would be turned into an increment measured
   * from today's position and quietly mean something else.
   */
  protected readonly modeOptions = computed(() => [
    { label: 'Pages read', value: 'pagesRead' as EntryMode, disabled: false },
    { label: 'To page', value: 'toPage' as EntryMode, disabled: !this.isToday() },
  ]);

  protected readonly amountLabel = computed(() =>
    this.mode() === 'pagesRead' ? 'Pages read' : 'Page reached',
  );

  /**
   * Says why the button is greyed out. Without it the field simply turns red and the reader is
   * left to work out the rule — and the backend's own explanation never arrives, because the
   * request is stopped before it is sent.
   */
  protected readonly validationMessage = computed(() => {
    const amount = this.amountValue();

    // Nothing typed yet is not a mistake worth pointing at.
    if (amount === null || amount === undefined) {
      return null;
    }

    if (this.mode() === 'toPage') {
      const current = this.currentPage() ?? 0;

      return amount <= current
        ? `You are already on page ${current}. Enter a page further along.`
        : null;
    }

    return amount <= 0 ? 'Enter a number greater than 0.' : null;
  });

  constructor() {
    // A position must be further along than where the reader already is; an increment stands on
    // its own. The backend enforces both — this only spares the round trip.
    effect(() => {
      const current = this.currentPage() ?? 0;
      const amount = this.form.controls.amount;

      amount.setValidators(
        this.mode() === 'toPage'
          ? [Validators.required, Validators.min(current + 1)]
          : [Validators.required, Validators.min(1)],
      );
      amount.updateValueAndValidity({ emitEvent: false });
    });
  }

  /**
   * The signal is what the rules above watch; the control is what the toggle is bound to.
   * Switching also clears the number, because carrying it over would silently turn "30 pages"
   * into "page 30".
   */
  protected onModeChange(mode: EntryMode): void {
    this.mode.set(mode);
    this.form.controls.amount.reset(null);
    this.errorMessage.set(null);
  }

  /**
   * Picking an earlier day takes "To page" away, so the mode falls back rather than leaving a
   * disabled option selected.
   */
  protected onDateChange(value: string): void {
    this.date.set(value);

    if (!this.isToday() && this.mode() === 'toPage') {
      this.form.controls.mode.setValue('pagesRead');
      this.onModeChange('pagesRead');
    }
  }

  protected onSubmit(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }

    const { mode, amount, date } = this.form.getRawValue();
    if (amount === null) {
      return;
    }

    const request: LogProgressRequest = {
      userBookId: this.userBookId(),
      date,
      ...(mode === 'pagesRead' ? { pagesRead: amount } : { toPage: amount }),
    };

    this.saving.set(true);
    this.errorMessage.set(null);

    this.readingLogService.logProgress(request).subscribe({
      next: (response) => {
        this.saving.set(false);

        // The date stays as it was: someone catching up on a forgotten week logs several books
        // for the same day in a row.
        this.form.controls.amount.reset(null);
        this.logged.emit(response);
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(
          err.error?.title ?? 'Could not save your progress. Please try again.',
        );
      },
    });
  }

  /**
   * Built from local date parts rather than `toISOString()`, which would send the UTC day and
   * date the entry to yesterday for anyone logging progress late in the evening.
   */
  private today(): string {
    return this.dateDaysAgo(0);
  }

  private dateDaysAgo(days: number): string {
    const date = new Date();
    date.setDate(date.getDate() - days);

    const pad = (value: number) => String(value).padStart(2, '0');

    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  }
}
