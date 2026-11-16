public partial class Scenario
{
    public ScenarioRender? Render { get; set; }
}

public partial class ScenarioRender
{
    public int? FontSize { get; set; }
    public string? FontFamily { get; set; }
    public string? Window { get; set; }
    public string? WindowTitle { get; set; }
    public string? Background { get; set; }
    public int? Margin { get; set; }
    public int? Padding { get; set; }
    public double? Opacity { get; set; }
    public int? Blur { get; set; }
    public bool? Loop { get; set; }
    public double? EndHold { get; set; }
    public double? FadeOut { get; set; }
    public string? Fit { get; set; }
    public int? CropTop { get; set; }
    public int? CropBottom { get; set; }
    public int? CropLeft { get; set; }
    public int? CropRight { get; set; }
    public ScenarioTheme? Theme { get; set; }
}

public partial class ScenarioTheme
{
    public string? Preset { get; set; }
    public string? Fg { get; set; }
    public string? Bg { get; set; }
    public string? Palette { get; set; }
}
