using System.Collections.Generic;

namespace BuilderAssistantApi.Domain.Constants;

/// <summary>
/// Authoritative source of truth for feature keys.
/// </summary>
public static class FeatureKeys
{
    public const string OcrScan = "ocr_scan";
    public const string HighRateApi = "high_rate_api";

    public static readonly IReadOnlyList<string> All = [OcrScan, HighRateApi];
}
