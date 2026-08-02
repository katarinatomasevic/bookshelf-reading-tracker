using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Shelf;

/// <summary>
/// A partial update of one shelf entry: every field is optional and only what is sent is
/// written. That leaves no way to express "clear this field", since an absent field and a null
/// one look the same in JSON, so three fields carry a sentinel instead:
/// <see cref="Note"/> and the two dates clear on an empty string, <see cref="Rating"/> on 0.
/// <para>
/// The dates are strings for exactly that reason — a <c>DateOnly?</c> could not tell "leave it"
/// from "clear it". <see cref="Today"/> comes from the client because the automatic dates below
/// must follow the reader's calendar day: a server using UTC would stamp yesterday on someone
/// finishing a book at 01:30 local time.
/// </para>
/// <para>
/// <see cref="PageCount"/> belongs to the shared Book row rather than to this user's entry, and
/// is accepted only to fill a gap Open Library left — never to overwrite a value another user
/// may already rely on.
/// </para>
/// </summary>
public record UpdateUserBookRequest(
    ReadingStatus? Status,
    int? Rating,
    string? Note,
    string? StartedAt,
    string? FinishedAt,
    int? PageCount,
    DateOnly? Today);
