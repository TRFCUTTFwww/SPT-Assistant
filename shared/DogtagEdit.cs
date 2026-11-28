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

        string? side = Side?.Trim().ToUpperInvariant() switch
        {
            null or "" => null,
            "USEC" => "Usec",
            "BEAR" => "Bear",
            _ => throw new ArgumentException("狗牌阵营须为 USEC 或 BEAR。")
        };

        string? utc = null;
        if (Time != null)
        {
            if (!DateTimeOffset.TryParseExact(Time.Trim(), TimeFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var time)
                && !DateTimeOffset.TryParse(Time.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out time))
                throw new ArgumentException("狗牌时间格式为 yyyy-MM-dd HH:mm:ss（UTC），也可输入 ISO 8601 时间。");

            utc = FormatTime(time.UtcDateTime);
        }
        return this with { Time = utc, Side = side };
    }

    public static string FormatTime(DateTime utc) => utc.ToString("O", CultureInfo.InvariantCulture);
}
