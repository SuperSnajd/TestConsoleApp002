using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FinalTestLogIngest.Parsing.Models;

namespace FinalTestLogIngest.Parsing;

/// <summary>
/// Parser for FinalTest log files using sv-SE culture for decimal parsing
/// </summary>
public static class FinalTestLogParser
{
    private static readonly CultureInfo SvCulture = CultureInfo.GetCultureInfo("sv-SE");

    /// <summary>
    /// Parses a FinalTest log file into a structured FinalTestLog object
    /// </summary>
    /// <param name="rawText">Complete raw text of the log file</param>
    /// <param name="fileName">Original filename for metadata</param>
    /// <returns>Parsed FinalTestLog object</returns>
    /// <exception cref="ArgumentException">Thrown when required fields are missing or malformed</exception>
    public static FinalTestLog Parse(string rawText, string fileName)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            throw new ArgumentException("Raw text cannot be null or empty", nameof(rawText));
        }

        var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var log = new FinalTestLog
        {
            RawText = rawText,
            Source = new SourceMetadata
            {
                OriginalFileName = fileName,
                IngestedAtUtc = DateTime.UtcNow
            }
        };

        int index = 0;

        // Parse header section
        ParseHeader(lines, ref index, log);

        // Parse test summary
        ParseTestSummary(lines, ref index, log);

        // Parse current result
        ParseCurrentResult(lines, ref index, log);

        // Parse all Signal Strength Data blocks
        ParseSignalStrengthBlocks(lines, ref index, log);

        // Build deterministic ID
        BuildId(log);

        // Set TimestampLocal
        log.TimestampLocal = log.Identity.Date.Add(log.Identity.Time);

        return log;
    }

    private static void ParseHeader(string[] lines, ref int index, FinalTestLog log)
    {
        // Parse identity and header fields
        while (index < lines.Length)
        {
            var line = lines[index].Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            // Stop when we reach the test summary section
            if (line.StartsWith("Time for test:", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            // Parse identity fields
            if (TryExtractValue(line, "Device Serial Number:", out var deviceSerial))
            {
                log.Identity.DeviceSerial = deviceSerial;
            }
            else if (TryExtractValue(line, "Date:", out var dateStr))
            {
                if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, 
                    DateTimeStyles.None, out var date))
                {
                    log.Identity.Date = date;
                }
                else
                {
                    throw new ArgumentException($"Failed to parse date: {dateStr}");
                }
            }
            else if (TryExtractValue(line, "Time:", out var timeStr))
            {
                if (TimeSpan.TryParseExact(timeStr, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var time))
                {
                    log.Identity.Time = time;
                }
                else
                {
                    throw new ArgumentException($"Failed to parse time: {timeStr}");
                }
            }
            // Parse header fields
            else if (TryExtractValue(line, "Test Operator:", out var testOperator))
            {
                log.Header.TestOperator = testOperator;
            }
            else if (TryExtractValue(line, "MCU Serial Number:", out var mcuSerial))
            {
                log.Header.McuSerialNumber = mcuSerial;
            }
            else if (TryExtractValue(line, "RF Unit Serial Number:", out var rfSerial))
            {
                log.Header.RfUnitSerialNumber = rfSerial;
            }
            else if (TryExtractValue(line, "Reader Type:", out var readerType))
            {
                log.Header.ReaderType = readerType;
            }
            else if (TryExtractValue(line, "Tagmod Version:", out var tagmodVersion))
            {
                log.Header.TagmodVersion = tagmodVersion;
            }
            else if (TryExtractValue(line, "RFsweep build date:", out var rfBuildDate))
            {
                log.Header.RfSweepBuildDate = rfBuildDate;
            }
            else if (TryExtractValue(line, "Config and Limit File Version:", out var configVersion))
            {
                log.Header.ConfigLimitFileVersion = configVersion;
            }
            else if (TryExtractValue(line, "Read Level:", out var readLevelStr))
            {
                if (int.TryParse(readLevelStr, out var readLevel))
                {
                    log.Header.ReadLevel = readLevel;
                }
            }

            index++;
        }
    }

    private static void ParseTestSummary(string[] lines, ref int index, FinalTestLog log)
    {
        // Find "Time for test:" line
        while (index < lines.Length)
        {
            var line = lines[index].Trim();

            if (line.StartsWith("Time for test:", StringComparison.OrdinalIgnoreCase))
            {
                // Format: "Time for test: 	147 seconds 	Pass/Fail: 	PASS"
                var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Find "seconds" keyword
                for (int i = 0; i < parts.Length; i++)
                {
                    if (int.TryParse(parts[i], out var seconds))
                    {
                        log.Summary.TimeForTestSeconds = seconds;
                    }
                    else if (parts[i].Equals("PASS", StringComparison.OrdinalIgnoreCase) || 
                             parts[i].Equals("FAIL", StringComparison.OrdinalIgnoreCase))
                    {
                        log.Summary.OverallPassFail = parts[i].ToUpperInvariant();
                    }
                }
                index++;
                break;
            }
            index++;
        }
    }

    private static void ParseCurrentResult(string[] lines, ref int index, FinalTestLog log)
    {
        // Find "Current result:" line
        while (index < lines.Length)
        {
            var line = lines[index].Trim();

            if (line.StartsWith("Current result:", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                // Next line should be: "Measured current:	0,174719	LimitLow:	0,130000	LimitHigh:	0,220000	Unit:	A	Pass/Fail:	PASS"
                if (index < lines.Length)
                {
                    var dataLine = lines[index];
                    var parts = dataLine.Split('\t', StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (parts[i].Contains("Measured current:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (decimal.TryParse(parts[i + 1], NumberStyles.Number, SvCulture, out var measured))
                            {
                                log.Current.MeasuredCurrent = measured;
                            }
                        }
                        else if (parts[i].Contains("LimitLow:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (decimal.TryParse(parts[i + 1], NumberStyles.Number, SvCulture, out var limitLow))
                            {
                                log.Current.LimitLow = limitLow;
                            }
                        }
                        else if (parts[i].Contains("LimitHigh:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (decimal.TryParse(parts[i + 1], NumberStyles.Number, SvCulture, out var limitHigh))
                            {
                                log.Current.LimitHigh = limitHigh;
                            }
                        }
                        else if (parts[i].Contains("Unit:", StringComparison.OrdinalIgnoreCase) && 
                                 !parts[i].Contains("RF Unit", StringComparison.OrdinalIgnoreCase))
                        {
                            log.Current.Unit = parts[i + 1];
                        }
                        else if (parts[i].Contains("Pass/Fail:", StringComparison.OrdinalIgnoreCase))
                        {
                            log.Current.PassFail = parts[i + 1].ToUpperInvariant();
                        }
                    }
                    index++;
                }
                break;
            }
            index++;
        }
    }

    private static void ParseSignalStrengthBlocks(string[] lines, ref int index, FinalTestLog log)
    {
        while (index < lines.Length)
        {
            var line = lines[index].Trim();

            if (line.Equals("Signal Strength Data", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                var block = ParseSignalStrengthBlock(lines, ref index);
                log.SignalStrengthBlocks.Add(block);
            }
            else
            {
                index++;
            }
        }
    }

    private static SignalStrengthData ParseSignalStrengthBlock(string[] lines, ref int index)
    {
        var block = new SignalStrengthData();

        while (index < lines.Length)
        {
            var line = lines[index].Trim();

            // Stop if we hit the next block or end
            if (line.Equals("Signal Strength Data", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            // Output power line
            if (line.StartsWith("Output power:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (int.TryParse(parts[i], out var power))
                    {
                        block.OutputPower = power;
                    }
                    else if (parts[i].Equals("PASS", StringComparison.OrdinalIgnoreCase) || 
                             parts[i].Equals("FAIL", StringComparison.OrdinalIgnoreCase))
                    {
                        block.PassFail = parts[i].ToUpperInvariant();
                    }
                }
                index++;
            }
            // Comparison header line
            else if (line.StartsWith("Comparison:", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                // Next line has the actual comparison data
                if (index < lines.Length)
                {
                    block.Comparison = ParseComparison(lines[index]);
                    index++;
                }
            }
            // Frequency Array
            else if (line.StartsWith("Frequency Array:", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index < lines.Length)
                {
                    block.FrequencyArray = ParseIntArray(lines[index]);
                    index++;
                }
            }
            // Signal Strength Matrix
            else if (line.StartsWith("Signal Strength Matrix", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                block.SignalStrengthMatrix = ParseMatrix(lines, ref index);
            }
            // Average Signal Strength
            else if (line.StartsWith("Average Signal Strength", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index < lines.Length)
                {
                    block.AverageSignalStrength = ParseIntArray(lines[index]);
                    index++;
                }
            }
            // Attenuation Array
            else if (line.StartsWith("Attenuation Array", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index < lines.Length)
                {
                    block.AttenuationArray = ParseIntArray(lines[index]);
                    index++;
                }
            }
            else
            {
                index++;
            }
        }

        return block;
    }

    private static Comparison ParseComparison(string line)
    {
        var comparison = new Comparison();
        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return comparison;
        }

        // Determine if first field is comparison type or numeric
        int fieldOffset = 0;
        if (!decimal.TryParse(parts[0].Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            // First field is comparison type (e.g., "GELE")
            comparison.ComparisonType = parts[0];
            fieldOffset = 1;
        }

        // Parse remaining fields: LimitLow, LimitHigh, Measurement, Unit, PassFail
        if (parts.Length > fieldOffset)
        {
            if (decimal.TryParse(parts[fieldOffset], NumberStyles.Number, SvCulture, out var limitLow))
            {
                comparison.LimitLow = limitLow;
            }
        }
        if (parts.Length > fieldOffset + 1)
        {
            if (decimal.TryParse(parts[fieldOffset + 1], NumberStyles.Number, SvCulture, out var limitHigh))
            {
                comparison.LimitHigh = limitHigh;
            }
        }
        if (parts.Length > fieldOffset + 2)
        {
            if (decimal.TryParse(parts[fieldOffset + 2], NumberStyles.Number, SvCulture, out var measurement))
            {
                comparison.Measurement = measurement;
            }
        }
        if (parts.Length > fieldOffset + 3)
        {
            comparison.Unit = parts[fieldOffset + 3];
        }
        if (parts.Length > fieldOffset + 4)
        {
            comparison.PassFail = parts[fieldOffset + 4].ToUpperInvariant();
        }

        return comparison;
    }

    private static List<int> ParseIntArray(string line)
    {
        var result = new List<int>();
        
        // Split on whitespace (spaces or tabs)
        var parts = Regex.Split(line, @"\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        foreach (var part in parts)
        {
            if (int.TryParse(part, out var value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static List<List<int>> ParseMatrix(string[] lines, ref int index)
    {
        var matrix = new List<List<int>>();

        while (index < lines.Length)
        {
            var line = lines[index];

            // Stop on empty line or next section header
            if (string.IsNullOrWhiteSpace(line) || 
                line.TrimStart().StartsWith("Average", StringComparison.OrdinalIgnoreCase) ||
                line.TrimStart().StartsWith("Attenuation", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            // Parse this line as an array of integers
            var row = ParseIntArray(line);
            if (row.Count > 0)
            {
                matrix.Add(row);
            }

            index++;
        }

        return matrix;
    }

    private static bool TryExtractValue(string line, string label, out string value)
    {
        value = string.Empty;

        if (line.StartsWith(label, StringComparison.OrdinalIgnoreCase))
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex >= 0 && colonIndex < line.Length - 1)
            {
                value = line.Substring(colonIndex + 1).Trim();
                return true;
            }
        }

        return false;
    }

    private static void BuildId(FinalTestLog log)
    {
        if (string.IsNullOrWhiteSpace(log.Identity.DeviceSerial))
        {
            throw new ArgumentException("Device Serial Number is required to build ID");
        }

        var dateStr = log.Identity.Date.ToString("yyyyMMdd");
        var timeStr = log.Identity.Time.ToString(@"hhmmss");

        log.Id = $"{log.Identity.DeviceSerial}-{dateStr}-{timeStr}";
    }
}

