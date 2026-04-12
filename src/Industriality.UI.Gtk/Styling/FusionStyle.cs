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
    font-family: "Segoe UI", "Noto Sans", "DejaVu Sans";
    font-size: 10pt;
}

window {
    background-color: #2a2d31;
    color: #d7d9db;
}

.fusion-nav {
    background-color: #31353a;
    border-bottom: 1px solid #4a4f55;
    padding: 6px;
}

.fusion-nav button {
    min-height: 26px;
    padding: 3px 10px;
}

.fusion-nav button.selected {
    border-color: #4f8fd3;
    background-image: linear-gradient(to bottom, #454a51, #353a40);
}

.fusion-content {
    padding: 0;
}

.fusion-card,
.fusion-banner-card {
    background-color: #33373d;
    border: 1px solid #4d5258;
    border-radius: 3px;
}

.fusion-card {
    padding: 6px;
}

.fusion-banner-card {
    padding: 0;
}

.fusion-section-title {
    font-size: 10pt;
    font-weight: 600;
    color: #eceff1;
}

button {
    background-image: linear-gradient(to bottom, #454a51, #363b41);
    background-color: #3a3f45;
    border: 1px solid #5a6168;
    border-top-color: #6a7179;
    border-bottom-color: #515860;
    border-left-color: #60676f;
    border-right-color: #60676f;
    border-radius: 3px;
    color: #e3e6e8;
    padding: 4px 10px;
}

button:hover {
    background-image: linear-gradient(to bottom, #4b5158, #3b4148);
    border-color: #6a727b;
}

button:active {
    background-image: linear-gradient(to bottom, #32373c, #3e444b);
    border-top-color: #262b30;
    border-bottom-color: #616973;
}

button:focus,
entry:focus,
spinbutton:focus,
spinbutton entry:focus {
    border-color: #4f8fd3;
}

.fusion-action-row button,
.fusion-settings-actions button {
    min-height: 30px;
}

.fusion-memory-input {
    min-height: 24px;
    padding: 2px 6px;
}

.fusion-step-button {
    min-width: 26px;
    min-height: 26px;
    padding: 0;
}

.fusion-step-button image {
    margin: 0;
    padding: 0;
}

button label {
    text-shadow: none;
    font-weight: 600;
}

entry,
spinbutton,
spinbutton entry {
    background-image: none;
    background-color: #2e3237;
    border: 1px solid #565c63;
    border-top-color: #5a6169;
    border-bottom-color: #4b525a;
    border-left-color: #515860;
    border-right-color: #515860;
    border-radius: 3px;
    color: #e2e5e8;
    padding: 3px 6px;
}

entry selection,
spinbutton entry selection {
    background-color: #4f8fd3;
    color: #ffffff;
}

entry:disabled,
spinbutton:disabled,
button:disabled,
label:disabled {
    color: #8b9299;
}

spinbutton button,
spinbutton button.up,
spinbutton button.down {
    background-image: linear-gradient(to bottom, #43484f, #363b41);
    border: 1px solid #545b63;
    border-top-color: #616872;
    border-bottom-color: #4a5159;
    border-radius: 2px;
    color: #ffffff;
    min-width: 16px;
    min-height: 16px;
    padding: 0;
}

spinbutton button:hover,
spinbutton button.up:hover,
spinbutton button.down:hover {
    background-image: linear-gradient(to bottom, #4c5259, #3c4249);
}

spinbutton button:active,
spinbutton button.up:active,
spinbutton button.down:active {
    background-image: linear-gradient(to bottom, #30353a, #3d434a);
}

scrolledwindow {
    border: 1px solid #4d5258;
    border-radius: 3px;
    background-color: #30343a;
}

scrollbar {
    background-color: #2c3035;
    border: 0;
}

scrollbar slider {
    background-color: #4b5158;
    border: 1px solid #606870;
    border-radius: 3px;
    min-width: 8px;
    min-height: 8px;
}

scrollbar slider:hover {
    background-color: #5a6169;
}

scrollbar slider:active {
    background-color: #4f8fd3;
    border-color: #5ea0e6;
}

progressbar,
progressbar trough,
progressbar progress {
    min-height: 28px;
}

progressbar trough {
    background-color: #2c3035;
    border: 1px solid #606870;
    border-top-color: #6a7179;
    border-bottom-color: #515860;
    border-left-color: #60676f;
    border-right-color: #60676f;
    border-radius: 3px;
}

progressbar progress {
    background-color: #4f8fd3;
    border-top: 1px solid rgba(255, 255, 255, 0.35);
    border-bottom: 1px solid rgba(0, 0, 0, 0.18);
    border-radius: 3px;
}

progressbar text {
    color: #ffffff;
    font-weight: 700;
}

.fusion-progress-text {
    color: #ffffff;
    font-weight: 700;
}

.fusion-popup,
.fusion-popup dialog,
.fusion-popup-body,
.fusion-popup-body box,
.fusion-popup-body label {
    background-color: #2a2d31;
    color: #d7d9db;
}
""";
}
