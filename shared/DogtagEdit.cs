using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace LocalSpt.Shared;

public sealed record DogtagEdit(
    [property: JsonPropertyName("nickname")] string? Nickname = null,
    [property: JsonPropertyName("time")] string? Time = null,
    [property: JsonPropertyName("side")] string? Side = null,
    [property: JsonPropertyName("level")] int? Level = null,
    [property: JsonPropertyName("status")] string? Status = null,
    [property: JsonPropertyName("weaponName")] string? WeaponName = null)
{
    static readonly string[] TimeFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK"
    ];

    public DogtagEdit Validate()
    {
        if (Nickname == null && Time == null && Side == null && Level == null && Status == null && WeaponName == null)
            throw new ArgumentException("请至少选择一项狗牌参数。");
        if (Nickname is { } name && (string.IsNullOrWhiteSpace(name) || name.Length > 64 || HasControl(name)))
            throw new ArgumentException("狗牌名字须为 1 至 64 个字符的单行文字。");
        if (Status is { } status && (status.Length > 64 || HasControl(status)))
            throw new ArgumentException("狗牌状态须为最多 64 个字符的单行文字。");
        if (WeaponName is { } weaponName && (weaponName.Length > 128 || HasControl(weaponName)))
            throw new ArgumentException("狗牌击杀武器须为最多 128 个字符的单行文字。");

        string? side = Side?.Trim().ToUpperInvariant() switch
        {
            null => null,
            "USEC" => "Usec",
            "BEAR" => "Bear",
            _ => throw new ArgumentException("狗牌阵营须为 USEC 或 BEAR。")
        };

        string? utc = null;
        if (Time != null)
        {
            if (!DateTimeOffset.TryParseExact(Time.Trim(), TimeFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var time))
                throw new ArgumentException("狗牌时间格式为 yyyy-MM-dd HH:mm:ss（UTC），也可输入 ISO 8601 时间。");

            utc = FormatTime(time.UtcDateTime);
        }
        return this with { Time = utc, Side = side };
    }

    public static string FormatTime(DateTime utc) => utc.ToString("O", CultureInfo.InvariantCulture);

    static bool HasControl(string value)
    {
        foreach (char character in value)
            if (char.IsControl(character)) return true;
        return false;
    }
}
