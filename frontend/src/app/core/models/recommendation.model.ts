import { BookCardData } from './book.model';

/** The reader's own book a recommendation was derived from. */
export interface BasedOn {
  bookId: string;
  title: string;
}

/**
 * One recommended book. Extends {@link BookCardData} so the shared card renders it unchanged.
 *
 * `bookId` is our own id rather than an Open Library key: every recommendation already exists as
 * a row in the database, so "add to shelf" posts the id directly. It is also what makes a
 * manually added book recommendable — such a book has no Open Library key at all.
 */
export interface Recommendation extends BookCardData {
  bookId: string;
  openLibraryId: string | null;
  pageCount: number | null;
  subjects: string[] | null;
  /** Null for popular (cold-start) recommendations, which have no source book to credit. */
  basedOn: BasedOn | null;
  /** Returned by the API but deliberately not shown: the attribution says more than a number. */
  similarity: number;
}

export interface Recommendations {
  items: Recommendation[];
  /** True when the list is popular books rather than anything personal to this reader. */
  isColdStart: boolean;
}
