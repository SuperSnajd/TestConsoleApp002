using System;
using System.Collections.Generic;

namespace FinalTestLogIngest.Parsing.Models;

/// <summary>
/// Represents a complete FinalTest log document with all test data and metadata
/// </summary>
public class FinalTestLog
{
    /// <summary>
    /// Deterministic identifier built from DeviceSerial + Date + Time
    /// Format: {DeviceSerial}-{yyyyMMdd}-{HHmmss}
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Source file metadata
    /// </summary>
    public SourceMetadata Source { get; set; } = new();

    /// <summary>
    /// Identity fields used to construct the Id
    /// </summary>
    public IdentityFields Identity { get; set; } = new();

    /// <summary>
    /// Header information from the test log
    /// </summary>
    public HeaderFields Header { get; set; } = new();

    /// <summary>
    /// Overall test summary
    /// </summary>
    public TestSummary Summary { get; set; } = new();

    /// <summary>
    /// Current measurement result
    /// </summary>
    public CurrentResult Current { get; set; } = new();

    /// <summary>
    /// List of signal strength data blocks (repeating section)
    /// </summary>
    public List<SignalStrengthData> SignalStrengthBlocks { get; set; } = new();

    /// <summary>
    /// Complete raw text of the original log file
    /// </summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of the raw text for deduplication
    /// </summary>
    public string ContentSha256 { get; set; } = string.Empty;

    /// <summary>
    /// Version number for this document (starts at 1, incremented on replacement)
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// History of replaced versions (populated when document is updated with different content)
    /// </summary>
    public List<ReplacedVersion> ReplacedHistory { get; set; } = new();

    /// <summary>
    /// Local timestamp (from Identity.Date + Identity.Time) for indexing and queries
    /// </summary>
    public DateTime TimestampLocal { get; set; }
}

/// <summary>
/// Source file metadata
/// </summary>
public class SourceMetadata
{
    /// <summary>
    /// Original filename (without path)
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the file was ingested
    /// </summary>
    public DateTime IngestedAtUtc { get; set; }
}

/// <summary>
/// Identity fields extracted from the test log used to build the deterministic Id
/// </summary>
public class IdentityFields
{
    /// <summary>
    /// Device Serial Number
    /// </summary>
    public string DeviceSerial { get; set; } = string.Empty;

    /// <summary>
    /// Test date (yyyy-MM-dd)
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Test time (HH:mm:ss)
    /// </summary>
    public TimeSpan Time { get; set; }
}

/// <summary>
/// Header information from the test log
/// </summary>
public class HeaderFields
{
    /// <summary>
    /// Test Operator identifier
    /// </summary>
    public string TestOperator { get; set; } = string.Empty;

    /// <summary>
    /// MCU Serial Number
    /// </summary>
    public string McuSerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// RF Unit Serial Number
    /// </summary>
    public string RfUnitSerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Reader Type (e.g., "HD")
    /// </summary>
    public string ReaderType { get; set; } = string.Empty;

    /// <summary>
    /// Tagmod Version (e.g., "v1.10.4")
    /// </summary>
    public string TagmodVersion { get; set; } = string.Empty;

    /// <summary>
    /// RF Sweep build date (free-form string)
    /// </summary>
    public string RfSweepBuildDate { get; set; } = string.Empty;

    /// <summary>
    /// Config and Limit File Version
    /// </summary>
    public string ConfigLimitFileVersion { get; set; } = string.Empty;

    /// <summary>
    /// Read Level
    /// </summary>
    public int ReadLevel { get; set; }
}

/// <summary>
/// Overall test summary
/// </summary>
public class TestSummary
{
    /// <summary>
    /// Time for test in seconds
    /// </summary>
    public int TimeForTestSeconds { get; set; }

    /// <summary>
    /// Overall Pass/Fail result (e.g., "PASS", "FAIL")
    /// </summary>
    public string OverallPassFail { get; set; } = string.Empty;
}

/// <summary>
/// Current measurement result
/// </summary>
public class CurrentResult
{
    /// <summary>
    /// Measured current value (parsed with sv-SE culture - decimal comma)
    /// </summary>
    public decimal MeasuredCurrent { get; set; }

    /// <summary>
    /// Lower limit for current
    /// </summary>
    public decimal LimitLow { get; set; }

    /// <summary>
    /// Upper limit for current
    /// </summary>
    public decimal LimitHigh { get; set; }

    /// <summary>
    /// Unit (e.g., "A" for amperes)
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Pass/Fail result (e.g., "PASS", "FAIL")
    /// </summary>
    public string PassFail { get; set; } = string.Empty;
}

/// <summary>
/// Signal Strength Data block (repeating section in the log)
/// </summary>
public class SignalStrengthData
{
    /// <summary>
    /// Output power value
    /// </summary>
    public int OutputPower { get; set; }

    /// <summary>
    /// Pass/Fail result for this block
    /// </summary>
    public string PassFail { get; set; } = string.Empty;

    /// <summary>
    /// Comparison data (LimitLow, LimitHigh, Measurement, Unit, Pass/Fail)
    /// </summary>
    public Comparison? Comparison { get; set; }

    /// <summary>
    /// Frequency array (integers)
    /// </summary>
    public List<int> FrequencyArray { get; set; } = new();

    /// <summary>
    /// Signal Strength Matrix (2D array represented as list of rows)
    /// Each row is a list of integers
    /// </summary>
    public List<List<int>> SignalStrengthMatrix { get; set; } = new();

    /// <summary>
    /// Average Signal Strength values
    /// </summary>
    public List<int> AverageSignalStrength { get; set; } = new();

    /// <summary>
    /// Attenuation Array values
    /// </summary>
    public List<int> AttenuationArray { get; set; } = new();
}

/// <summary>
/// Comparison data within a Signal Strength Data block
/// </summary>
public class Comparison
{
    /// <summary>
    /// Comparison type (e.g., "GELE") or empty string
    /// </summary>
    public string ComparisonType { get; set; } = string.Empty;

    /// <summary>
    /// Lower limit (nullable for cases with just zeros/no comparison)
    /// </summary>
    public decimal? LimitLow { get; set; }

    /// <summary>
    /// Upper limit (nullable for cases with just zeros/no comparison)
    /// </summary>
    public decimal? LimitHigh { get; set; }

    /// <summary>
    /// Measured value (parsed with sv-SE culture - decimal comma)
    /// </summary>
    public decimal Measurement { get; set; }

    /// <summary>
    /// Unit (e.g., "dB")
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Pass/Fail result
    /// </summary>
    public string PassFail { get; set; } = string.Empty;
}

/// <summary>
/// Represents a previous version of the document that was replaced
/// </summary>
public class ReplacedVersion
{
    /// <summary>
    /// Version number that was replaced
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// SHA256 hash of the replaced content
    /// </summary>
    public string ContentSha256 { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this version was replaced
    /// </summary>
    public DateTime ReplacedAtUtc { get; set; }
}

