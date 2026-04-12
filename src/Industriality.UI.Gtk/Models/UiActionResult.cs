namespace Industriality.UI.Gtk.Models;

public sealed record UiActionResult(bool Success, string Message, string? Details = null)
{
    public static UiActionResult Ok(string message)
    {
        return new UiActionResult(true, message);
    }

    public static UiActionResult Fail(string message, string? details = null)
    {
        return new UiActionResult(false, message, details);
    }
}

public sealed record UiActionResult<T>(bool Success, string Message, T? Data, string? Details = null)
{
    public static UiActionResult<T> Ok(T data, string message)
    {
        return new UiActionResult<T>(true, message, data);
    }

    public static UiActionResult<T> Fail(string message, string? details = null, T? data = default)
    {
        return new UiActionResult<T>(false, message, data, details);
    }
}
