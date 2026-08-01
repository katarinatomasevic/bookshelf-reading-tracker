/** The subset of a book the shared card renders; both search results and shelf items satisfy it. */
export interface BookCardData {
  title: string;
  author: string | null;
  coverId: number | null;
}

export interface BookSearchResult extends BookCardData {
  openLibraryId: string;
  firstPublishYear: number | null;
  pageCount: number | null;
  subjects: string[] | null;
  isbn: string | null;
  /** Only meaningful for a logged-in user; always false for a guest. */
  isOnShelf: boolean;
}

export interface BookSearchPageResult {
  items: BookSearchResult[];
  page: number;
  hasMore: boolean;
}

export interface BookDetails extends BookCardData {
  /** Our own id — present only once the book has been saved (i.e. someone added it to a shelf). */
  id: string | null;
  openLibraryId: string | null;
  description: string | null;
  subjects: string[] | null;
  pageCount: number | null;
  isbn: string | null;
  averageRating: number | null;
  ratingsCount: number | null;
  isOnShelf: boolean;
}
