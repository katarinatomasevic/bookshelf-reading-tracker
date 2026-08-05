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
}
