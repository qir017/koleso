namespace Coleso;

/// <summary>
/// Оформление (тема) одного колеса: набор цветов для отрисовки диска,
/// секторов, подсветки и центральной надписи.
/// </summary>
public sealed class WheelTheme
{
    public string Key { get; init; } = "dark";
    public string Name { get; init; } = "Тёмная";

    public string Disc { get; init; } = "#C013161B";        // большой фон-диск
    public string DiscStroke { get; init; } = "#26FFFFFF";
    public string SectorFill { get; init; } = "#00000000";  // заливка сектора без картинки
    public string SectorStroke { get; init; } = "#38FFFFFF";
    public string HiFill { get; init; } = "#E8FFFFFF";      // подсветка выбранного сектора
    public string HiVeil { get; init; } = "#59FFFFFF";      // подсветка сектора, у которого есть картинка
    public string HiText { get; init; } = "#FF101418";      // текст сектора при подсветке
    public string LabelText { get; init; } = "#D8FFFFFF";
    public string CenterFill { get; init; } = "#E815181D";
    public string CenterStroke { get; init; } = "#33FFFFFF";
    public string NameText { get; init; } = "#E0FFFFFF";
    public string BadgeText { get; init; } = "#78FFFFFF";
    public string PreviewText { get; init; } = "#B8FFFFFF";
    public string HintText { get; init; } = "#70FFFFFF";
}

/// <summary>Набор встроенных тем. Тема задаётся ключом в config.json (<c>Wheel.Theme</c>).</summary>
public static class WheelThemes
{
    public const string DefaultKey = "dark";

    public static readonly WheelTheme Dark = new()
    {
        Key = "dark", Name = "Тёмная"
    };

    public static readonly WheelTheme Light = new()
    {
        Key = "light", Name = "Светлая",
        Disc = "#F2FFFFFF", DiscStroke = "#33000000",
        SectorFill = "#00000000", SectorStroke = "#40000000",
        HiFill = "#E61B64C7", HiVeil = "#661B64C7", HiText = "#FFFFFFFF",
        LabelText = "#FF1B1E24",
        CenterFill = "#FFFFFFFF", CenterStroke = "#40000000",
        NameText = "#FF101418", BadgeText = "#99101418",
        PreviewText = "#CC101418", HintText = "#70101418"
    };

    public static readonly WheelTheme Neon = new()
    {
        Key = "neon", Name = "Неон",
        Disc = "#D005070F", DiscStroke = "#6600E5FF",
        SectorFill = "#00000000", SectorStroke = "#5900E5FF",
        HiFill = "#B300E5FF", HiVeil = "#6600E5FF", HiText = "#FF03141C",
        LabelText = "#CCD9FFFF",
        CenterFill = "#F005070F", CenterStroke = "#66FF00E5",
        NameText = "#FF7DF9FF", BadgeText = "#99FF3EA5",
        PreviewText = "#CCE4F6FF", HintText = "#809FE7FF"
    };

    public static readonly WheelTheme Gold = new()
    {
        Key = "gold", Name = "Золото",
        Disc = "#E8211A11", DiscStroke = "#59FFCE6A",
        SectorFill = "#00000000", SectorStroke = "#66FFCE6A",
        HiFill = "#B3FFCE6A", HiVeil = "#66FFCE6A", HiText = "#FF241A08",
        LabelText = "#FFEED9A5",
        CenterFill = "#F0261E13", CenterStroke = "#66FFCE6A",
        NameText = "#FFFFEDBB", BadgeText = "#99FFCE6A",
        PreviewText = "#CCDCC494", HintText = "#80C9AC74"
    };

    public static readonly WheelTheme Forest = new()
    {
        Key = "forest", Name = "Лес",
        Disc = "#E80D1A11", DiscStroke = "#596FD98B",
        SectorFill = "#00000000", SectorStroke = "#596FD98B",
        HiFill = "#B36FD98B", HiVeil = "#666FD98B", HiText = "#FF0B1B10",
        LabelText = "#CCD7F5E0",
        CenterFill = "#F0102016", CenterStroke = "#666FD98B",
        NameText = "#FFC9F2D4", BadgeText = "#996FD98B",
        PreviewText = "#CCA9DCC0", HintText = "#807FBF9B"
    };

    public static readonly WheelTheme Cyber = new()
    {
        Key = "cyber", Name = "Киберпанк",
        Disc = "#E8140A24", DiscStroke = "#66FF2FB4",
        SectorFill = "#00000000", SectorStroke = "#66FF2FB4",
        HiFill = "#B3FF2FB4", HiVeil = "#66FF2FB4", HiText = "#FF1B0714",
        LabelText = "#CCF3D9FF",
        CenterFill = "#F0180C2B", CenterStroke = "#6639FFC3",
        NameText = "#FFFF7ADF", BadgeText = "#9939FFC3",
        PreviewText = "#CCDCC9F5", HintText = "#80B98FE0"
    };

    public static IReadOnlyList<WheelTheme> All { get; } =
        new[] { Dark, Light, Neon, Gold, Forest, Cyber };

    public static WheelTheme Get(string? key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Dark;

    public static bool IsValid(string? key) =>
        key != null && All.Any(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}
