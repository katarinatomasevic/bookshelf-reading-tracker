import { ShelfItem } from './shelf.model';

/**
 * A day's progress. Exactly one of `pagesRead` and `toPage` is sent — the reader picks which one
 * they are thinking in, and the backend turns a position into the increment the log stores.
 * `date` is the reader's own calendar day, never the server's.
 */
export interface LogProgressRequest {
  userBookId: string;
  date: string;
  pagesRead?: number;
  toPage?: number;
}

/**
 * The updated shelf entry, in the same shape a shelf PATCH returns, so the shelf signal is
 * refreshed through one path. `bookCompleted` only reports reaching the last page; changing the
 * status stays the reader's decision.
 */
export interface LogProgressResponse {
  item: ShelfItem;
  bookCompleted: boolean;
  /**
   * Everything now recorded for that date — not necessarily what was just entered, since a
   * second sitting on the same day adds to the entry already there. It is what lets the
   * confirmation name the day's real total instead of repeating the number back.
   */
  dayTotal: number;
}

/**
 * One day in a book's reading history. No `createdAt`: for a backdated entry that is a different
 * day from the one being reported, and showing both would raise a question the list need not ask.
 */
export interface ReadingLogEntry {
  id: string;
  date: string;
  pagesRead: number;
}

/**
 * Corrections carry the page count and nothing else — the date is deliberately not editable,
 * because moving an entry onto a day that already has one would force a merge nobody asked for.
 * A wrong day is fixed by deleting the entry and adding it again.
 */
export interface UpdateReadingLogRequest {
  pagesRead: number;
}
