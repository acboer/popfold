using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Popfold;

/// <summary>Janela de opções no estilo "Bala de Goma": transparência, vidro, estilo, idioma, iniciar com o Windows.</summary>
public partial class SettingsWindow : Window
{
    private readonly App _app;
    private bool _loading = true;

    private AppSettings S => _app.Config.Settings;

    public SettingsWindow(App app)
    {
        _app = app;
        InitializeComponent();

        TransparencySlider.Value = S.Transparency;
        GlassCheck.IsChecked = S.Glass;
        (S.Style == "dark" ? StyleDarkRadio : StyleLightRadio).IsChecked = true;
        (S.Language switch { "pt" => LangPtRadio, "en" => LangEnRadio, _ => LangAutoRadio }).IsChecked = true;
        try { StartupCheck.IsChecked = StartupManager.IsEnabled; } catch { }
        Localize();
        _loading = false;

        TransparencySlider.ValueChanged += (_, _) =>
        {
            S.Transparency = (int)TransparencySlider.Value;
            TransparencyValue.Text = $"{S.Transparency}%";
            _app.ApplyAppearance();
        };
        GlassCheck.Click += (_, _) => { S.Glass = GlassCheck.IsChecked == true; _app.ApplyAppearance(); };
        StyleLightRadio.Checked += (_, _) => SetStyle("light");
        StyleDarkRadio.Checked += (_, _) => SetStyle("dark");
        LangAutoRadio.Checked += (_, _) => SetLanguage("auto");
        LangPtRadio.Checked += (_, _) => SetLanguage("pt");
        LangEnRadio.Checked += (_, _) => SetLanguage("en");
        StartupCheck.Click += (_, _) => { try { StartupManager.Set(StartupCheck.IsChecked == true); } catch { } };
        CloseButton.Click += (_, _) => Close();
        Closed += (_, _) => _app.SaveConfig();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Barra de título nas cores da identidade (Windows 11; ignorado nas versões antigas).
        var hwnd = new WindowInteropHelper(this).Handle;
        int caption = 0x00F8F4FF; // #FFF4F8 (COLORREF = 0x00BBGGRR)
        int text = 0x004A1B2B;    // #2B1B4A
        DwmSetWindowAttribute(hwnd, 35 /* DWMWA_CAPTION_COLOR */, ref caption, sizeof(int));
        DwmSetWindowAttribute(hwnd, 36 /* DWMWA_TEXT_COLOR */, ref text, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void SetStyle(string style)
    {
        if (_loading) return;
        S.Style = style;
        _app.ApplyAppearance();
    }

    private void SetLanguage(string language)
    {
        if (_loading) return;
        _app.SetLanguage(language);
        Localize();
    }

    private void Localize()
    {
        Title = L.OptionsTitle;
        TaglineText.Text = L.Tagline;
        AppearanceHeader.Text = L.Appearance;
        TransparencyLabel.Text = L.FolderTransparency;
        TransparencyValue.Text = $"{S.Transparency}%";
        MoreSolidText.Text = L.MoreSolid;
        MoreClearText.Text = L.MoreTransparent;
        GlassLabel.Text = L.GlassEffect;
        GlassHintText.Text = L.GlassHint;
        StyleLabel.Text = L.Style;
        StyleLightRadio.Content = L.StyleLight;
        StyleDarkRadio.Content = L.StyleDark;
        GeneralHeader.Text = L.General;
        LanguageLabel.Text = L.Language;
        LangAutoRadio.Content = L.Auto;
        StartupLabel.Text = L.StartWithWindows;
        StartupHintText.Text = L.StartHint;
        TipText.Text = L.SelectionTip;
        CloseButton.Content = L.Close;
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"{AppInfo.Name} {v?.Major}.{v?.Minor}";
    }
}
