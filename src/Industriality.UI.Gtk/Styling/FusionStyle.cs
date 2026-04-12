using Gtk;

namespace Industriality.UI.Gtk.Styling;

public static class FusionStyle
{
    public static void Apply()
    {
        using var provider = new CssProvider();
        provider.LoadFromData(Css);
        StyleContext.AddProviderForScreen(
            Gdk.Screen.Default,
            provider,
            600);
    }

private const string Css = """
* {
    box-shadow: none;
    text-shadow: none;
    outline-color: transparent;
    animation: none;
    transition: none;
}

window {
    background-color: #2b2f34;
    color: #e7eaed;
}

.fusion-nav {
    background-color: #25292d;
    border-right: 1px solid #1e2126;
    padding: 12px;
}

.fusion-nav button {
    background-image: none;
    background-color: #30353a;
    border: 1px solid #3c434a;
    border-radius: 6px;
    color: #d9dde0;
    padding: 8px 10px;
}

.fusion-nav button:hover {
    background-color: #414a51;
    border-color: #5d6a75;
}

.fusion-nav button.selected {
    background-color: #3d515e;
    border-color: #506a7b;
}

.fusion-content {
    padding: 18px;
}

.fusion-card {
    background-color: #32383f;
    border: 1px solid #434b53;
    border-radius: 10px;
    padding: 14px;
}

.fusion-section-title {
    font-size: 18px;
    font-weight: 600;
}

.fusion-action-row button {
    min-height: 46px;
    background-image: none;
    background-color: #385264;
    border: 1px solid #4c6a80;
    border-radius: 7px;
    color: #e9f0f5;
}

.fusion-action-row button:hover {
    background-color: #4a667a;
    border-color: #6a879f;
}

.fusion-action-row button.secondary {
    background-color: #4e3f53;
    border-color: #6d5774;
}

.fusion-action-row button.secondary:hover {
    background-color: #63526b;
    border-color: #846f8c;
}

.fusion-settings-actions button {
    background-image: none;
    background-color: #3a4047;
    border: 1px solid #4d555f;
    border-radius: 6px;
    color: #e0e5e8;
}

.fusion-settings-actions button:hover {
    background-color: #4a525b;
    border-color: #636d78;
}

button label {
    text-shadow: none;
    font-weight: 600;
}

.fusion-input {
    background-color: #2a2f34;
    border: 1px solid #49525a;
    border-radius: 5px;
    color: #f0f3f5;
    padding: 6px 8px;
}
""";
}
