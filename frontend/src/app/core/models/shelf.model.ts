import { BookCardData } from './book.model';

/** Mirrors the backend enum; the labels shown to the user live in the shelf page. */
export enum ReadingStatus {
  WantToRead = 0,
  Reading = 1,
  Read = 2,
}

export interface ShelfItem extends BookCardData {
  /** The UserBook id — the shelf entry, not the book. */
  id: string;
  bookId: string;
  openLibraryId: string | null;
  pageCount: number | null;
  subjects: string[] | null;
  status: ReadingStatus;
  rating: number | null;
  note: string | null;
  currentPage: number | null;
  startedAt: string | null;
  finishedAt: string | null;
  addedAt: string;
}

/**
 * A partial update: only the fields present are written. Because an absent field and a null one
 * are the same thing in JSON, clearing a value has its own signal — an empty string for the note
 * and the two dates, 0 for the rating. `today` is the reader's own calendar day, which the
 * backend needs for the dates a status change sets automatically.
 */
export interface UpdateShelfItemRequest {
  status?: ReadingStatus;
  rating?: number;
  note?: string;
  startedAt?: string;
  finishedAt?: string;
  pageCount?: number;
  today?: string;
}

export interface ShelfCounts {
  wantToRead: number;
  reading: number;
  read: number;
}

/** Matches the backend's `?sort=` values, though the shelf sorts what it already has loaded. */
export type ShelfSort = 'added_desc' | 'title' | 'rating_desc' | 'finished_desc';

export interface AddToShelfRequest {
  openLibraryId?: string;
  bookId?: string;
  /** Passed on from the search result: the Open Library work endpoint returns neither. */
  pageCount?: number | null;
  isbn?: string | null;
}

export interface ManualBookRequest {
  title: string;
  author: string | null;
  description: string | null;
  pageCount: number | null;
  subjects: string[];
}
