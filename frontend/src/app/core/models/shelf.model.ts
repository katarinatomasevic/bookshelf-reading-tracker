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
