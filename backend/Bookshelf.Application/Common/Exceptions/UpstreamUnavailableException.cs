namespace Bookshelf.Application.Common.Exceptions;

/// <summary>
/// An external service the request depended on did not answer — it timed out, refused the
/// connection, or replied with something unusable.
///
/// The distinction this type exists to make is between "we are broken" and "they are broken".
/// Without it, Open Library being slow surfaces as HTTP 500, which tells the reader our
/// application has a defect and tells us to go looking for one. It is a 503: the request was
/// fine, the code is fine, and trying again shortly is genuinely likely to work.
/// </summary>
public class UpstreamUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
