public partial class Scenario
{
    public ScenarioRender? Render { get; set; }
}

public partial class ScenarioRender
{
    public int? FontSize { get; set; }
    public string? FontFamily { get; set; }
    public string? Window { get; set; }
    public ScenarioTheme? Theme { get; set; }
}

public partial class ScenarioTheme
{
    public string? Preset { get; set; }
    public string? Fg { get; set; }
    public string? Bg { get; set; }
    public string? Palette { get; set; }
}
