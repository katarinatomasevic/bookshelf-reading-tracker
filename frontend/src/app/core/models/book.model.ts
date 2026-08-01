export interface BookSearchResult {
  openLibraryId: string;
  title: string;
  author: string | null;
  firstPublishYear: number | null;
  coverId: number | null;
  pageCount: number | null;
  subjects: string[] | null;
  isbn: string | null;
}

export interface BookSearchPageResult {
  items: BookSearchResult[];
  page: number;
  hasMore: boolean;
}

export interface BookDetails {
  openLibraryId: string;
  title: string;
  author: string | null;
  description: string | null;
  coverId: number | null;
  subjects: string[] | null;
  averageRating: number | null;
  ratingsCount: number | null;
}
