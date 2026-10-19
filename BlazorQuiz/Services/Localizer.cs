using Microsoft.AspNetCore.Components;

namespace BlazorQuiz.Services;

/// <summary>
/// Per-circuit UI language. Indonesian is the source language: pages call <c>L.T("teks Indonesia", "English text")</c>.
/// The choice lives in the <see cref="CookieName"/> cookie, written by the /set-language endpoint.
/// </summary>
public interface ILocalizer
{
    string Language { get; }
    bool IsEnglish { get; }
    string T(string id, string en);
}

public sealed class Localizer : ILocalizer, IDisposable
{
    public const string CookieName = "ui-lang";
    private const string StateKey = "ui-lang";

    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription _subscription;

    public string Language { get; private set; } = "id";
    public bool IsEnglish => Language == "en";

    public Localizer(IHttpContextAccessor httpContextAccessor, PersistentComponentState state)
    {
        _state = state;

        // Interactive circuits have no HttpContext, so the prerender hands the language over through persisted state.
        if (_state.TryTakeFromJson<string>(StateKey, out var restored) && IsSupported(restored))
        {
            Language = restored!;
        }
        else
        {
            var cookie = httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
            if (IsSupported(cookie))
            {
                Language = cookie!;
            }
        }

        _subscription = _state.RegisterOnPersisting(() =>
        {
            _state.PersistAsJson(StateKey, Language);
            return Task.CompletedTask;
        });
    }

    public string T(string id, string en) => IsEnglish ? en : id;

    public static bool IsSupported(string? language) => language is "id" or "en";

    public void Dispose() => _subscription.Dispose();
}
