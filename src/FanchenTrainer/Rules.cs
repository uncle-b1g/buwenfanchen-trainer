using System.Globalization;

namespace FanchenTrainer;

// Pure validation is shared with the small offline test harness.
internal static class Rules
{
    public static float Number(string text, float min = 0, float max = 10_000_000)
    {
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !float.IsFinite(value) || value < min || value > max)
            throw new ArgumentException($"请输入 {min} 到 {max} 之间的数字，小数点使用英文句点。");
        return value;
    }

    public static int Integer(string text, int min, int max)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
            throw new ArgumentException($"请输入 {min} 到 {max} 之间的整数。");
        return value;
    }

    public static bool Matches(string name, int id, string query) =>
        name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) ||
        id.ToString(CultureInfo.InvariantCulture).Contains(query.Trim(), StringComparison.Ordinal);

    public static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
