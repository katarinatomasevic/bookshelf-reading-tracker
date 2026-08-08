/**
 * Matches the backend's `?period=` values. Only the cards and the two charts follow it — the
 * streak and the activity grid always cover the same span whatever is selected here.
 */
export type DashboardPeriod = 'last_30_days' | 'this_year' | 'last_year' | 'all_time';

export interface Streak {
  current: number;
  longest: number;
}

export interface DashboardStats {
  booksRead: number;
  /** Finished books count in full, books in progress only their logged pages. */
  totalPages: number;
  /** Logged pages divided by the days they were logged on — deliberately not totalPages. */
  averagePagesPerDay: number;
}

/** Months once the range grows past two years, so a decade of reading stays readable. */
export type BooksFinishedGranularity = 'month' | 'year';

export interface BooksFinished {
  year: number;
  /** 1-12, as sent by the backend (not the zero-based month a JS Date uses); null per year. */
  month: number | null;
  count: number;
}

export interface GenreCount {
  subject: string;
  count: number;
}

export interface DashboardSummary {
  period: DashboardPeriod;
  streak: Streak;
  stats: DashboardStats;
  /** What one bar stands for; the chart's heading follows it. */
  booksFinishedGranularity: BooksFinishedGranularity;
  /** Every bucket in the period, including the empty ones the backend filled with zeros. */
  booksFinished: BooksFinished[];
  topGenres: GenreCount[];
}

export interface ActivityDay {
  date: string;
  pages: number;
}

/** 26 whole weeks, Monday to Sunday, every day present — 182 entries in grid order. */
export interface Activity {
  from: string;
  to: string;
  days: ActivityDay[];
}
