using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CommandLine.Common;

namespace SetBackground
{
    internal sealed class LogHelper
    {
        private static readonly Regex SonarProjectUrlRegex = new Regex("ANALYSIS SUCCESSFUL, you can browse (?<url>http.+)$");

        private LogHelper()
        {
        }

        // Example:
        //     27>C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Microsoft\VisualStudio\v16.0\TeamTest\Microsoft.TeamTest.targets(14,5): error : Could not load file or assembly 'file:///C:\Users\C208670\Source\Repos\SalesServices\src\BusinessLogic.Tests\Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll' or one of its dependencies. The system cannot find the file specified. [C:\Users\C208670\Source\Repos\SalesServices\src\BusinessLogic.Tests\BusinessLogic.Tests.csproj]
        private static readonly Regex MissingFileRegex = new Regex(@"Could not load file or assembly '(file:///)?(?<filePath>[^']+)'", RegexOptions.IgnoreCase);

        private static readonly object SyncLock = new object();

        internal static ConcurrentQueue<string> MessageQueue { get; } = new ConcurrentQueue<string>();

        internal static string SonarProjectUrl { get; private set; }

        internal static UserArguments UserArguments { get; set; }

        internal static bool ShouldJustPrintCommands { get; set; }

        public static void LoggerCallback(LogType? logType, string[] lines)
        {
            foreach (var line in lines)
            {
                LoggerCallback(logType, line);
            }
        }

        public static void LoggerCallback(LogType? logTypeArg, string line)
        {
            var logType = logTypeArg ?? LogHelper.DetermineLogTypeForOutputLine(line);

            if (ShouldJustPrintCommands)
            {
                switch (logType)
                {
                    case LogType.Command:
                        LogHelper.SafeConsoleWriteLine(LogHelper.AdjustCommandLineForDisplay(line));
                        break;
                    case LogType.Warning:
                        LogHelper.SafeConsoleWriteLine($"REM Warning. {line}");
                        break;
                    case LogType.Error:
                        LogHelper.SafeConsoleWriteLine($"REM Error. {line}");
                        break;
                }
                return;
            }

            if (!LogHelper.ShowForVerboseLevel(logType))
            {
                return;
            }

            LogHelper.TryToExtractSonarProjectUrl(line);
            LogHelper.DetermineColorAndPrefix(logType, out var foregroundColor, out var prefix);

            var formattedPrefix = (prefix != null) ? $"{prefix,-7} " : "";

            LogHelper.SafeConsoleWriteLine(line, foregroundColor, formattedPrefix);
        }

        /// <summary>
        /// Adjust the provide command line statement to a displayable format that the developer
        /// can use to understand and re-issue the command more easily.
        /// The main case is for command line commands that don't need to have the "cmd.exe /C"
        /// prefix, since the developer would be issuing the commands from a command line anyway.
        /// </summary>
        /// <remarks>
        /// <example><code>"cmd.exe" /C rmdir bin</code> becomes <code>rmdir bin</code></example>
        /// </remarks>
        /// <param name="line">Output line such as <code>"cmd.exe" /C rmdir bin</code></param>
        /// <returns>line without the command shell prefix</returns>
        public static string AdjustCommandLineForDisplay(string line)
        {
            if (line.StartsWith("\"cmd.exe\" /C ", true, CultureInfo.InvariantCulture))
            {
                line = line.Substring(13);
            }

            return line;
        }

        public static bool ShowForVerboseLevel(LogType logType)
        {
            if (UserArguments == null || UserArguments.Verbose)
            {
                return true;
            }

            switch (logType)
            {
                case LogType.Command:
                case LogType.Verbose:
                case LogType.ExitCode:
                    return false;

                default:
                    return true;
            }
        }

        public static LogType DetermineLogTypeForOutputLine(string outputLine)
        {
            if (outputLine != null)
            {
                var outputLineLowercased = outputLine.ToLower();
                var outputLineStartTrimmed = outputLine.TrimStart();

                // Example:
                //   Error Message:
                //    Assert.AreEqual failed. Expected:< (null) >.Actual:< ACCESS_REFUSED - Login was refused using authentication mechanism PLAIN.For details see the broker logfile.>.Expected the error message to be empty(no errors) for the verification item, but there was an error with message: ACCESS_REFUSED - Login was refused using authentication mechanism PLAIN.For details see the broker logfile.
                if (HasStatusMessage(outputLineLowercased, "error"))
                {
                    return LogType.Error;
                }
                // Example:
                // Managers\OasisToAdcMappers\OasisToDuckCreekMapper.cs(1093,38): warning S3240: Use the '??' operator here. [C:\Users\C208670\Source\Repos\PcnvTransformMapping\src\DomainLayer\DomainLayer.csproj]
                if (HasStatusMessage(outputLineLowercased, "warning"))
                {
                    return LogType.Warning;
                }

                // Examples:
                // WARN: Forced reloading of SCM data for all files.
                if (outputLineLowercased.TrimStart().StartsWith("warn:"))
                {
                    return LogType.Warning;
                }

                // Examples:
                // 12:33:56.193  Pre-processing succeeded.
                // Build succeeded.
                if (HasWordInMessage(outputLineLowercased, "succeeded"))
                {
                    return LogType.Success;
                }
                if (HasWordInMessage(outputLineLowercased, "successful"))
                {
                    return LogType.Success;
                }
                if (HasWordInMessage(outputLineLowercased, "localhost"))
                {
                    return LogType.Info;
                }

                // ---------------------
                // Unit Test examples...
                // ---------------------
                // Success example:
                //   √ PolicyTermTransactionProcessor_GetCumulativePolicyTermInTransaction_WhenMultiplePolicyTermsExistInATransaction_ShouldThrowException [37ms]
                // Disabled example:
                //   ! ProAVerification_VerificationManager_WithMultipleTestPolicyData_ShouldVerifyWithoutAnyErrors
                // Failure example: 
                //   X DomainFacade_ConvertOasisPolicyToAdcAsync_WithNullPolicy_ShouldThrowInvalidPolicyNumberException [364ms]
                // Failed   LookupCreditScoreVendorControl_CycleTestNull
                if (outputLineStartTrimmed.StartsWith("X ") || outputLineStartTrimmed.StartsWith("Failed "))
                {
                    return LogType.Error;
                }
                if (outputLineStartTrimmed.StartsWith("! ") || outputLineStartTrimmed.StartsWith("Skipped "))
                {
                    return LogType.Warning;
                }
                // Summary example:
                // Test Run Failed.
                // Total tests: 1227
                //      Passed: 952
                //      Failed: 83
                //     Skipped: 192
                // Total time: 4.7731 Minutes
                if (HasWordInMessage(outputLineLowercased, "passed"))
                {
                    return LogType.Success;
                }
                if (HasWordInMessage(outputLineLowercased, "failed"))
                {
                    return LogType.Error;
                }
                if (HasWordInMessage(outputLineLowercased, "skipped"))
                {
                    return LogType.Warning;
                }
            }

            return LogType.Normal;
        }

        private static bool HasWordInMessage(string messageLowercased, string partialPatternLowercased)
        {
            // Examples to match as warnings/errors/etc..
            // (Note the words "warning" and "error" in the line)
            //      4>Bin\Microsoft.Common.CurrentVersion.targets(2106,5): warning MSB3270: There was a mismatch between the processor ...
            //      CSC : error CS0016: Could not write to output file ...
            //      Error CS0016: Could not write to output file ...
            //      1 Warning(s)
            //      1 Error(s)
            var containsPatternRegex = new Regex($@"\b{partialPatternLowercased}\b");

            // Examples to NOT match as warnings or errors:
            //      0 Warning(s)
            //      0 Error(s)
            var zeroWarningsErrorsRegex = new Regex($@"[^\d]0\s+{partialPatternLowercased}\(s\)");

            return containsPatternRegex.IsMatch(messageLowercased) &&
                   !zeroWarningsErrorsRegex.IsMatch(messageLowercased);
        }

        private static bool HasStatusMessage(string messageLowercased, string partialPatternLowercased)
        {
            // Examples to match as warnings/errors/etc..
            // (Note the words "warning" and "error" in the line)
            //      4>Bin\Microsoft.Common.CurrentVersion.targets(2106,5): warning MSB3270: There was a mismatch between the processor ...
            //      CSC : error CS0016: Could not write to output file ...
            //      Error CS0016: Could not write to output file ...
            //      1 Warning(s)
            //      1 Error(s)
            return new Regex($@"\b{partialPatternLowercased} [A-Z]+[0-9]+:", RegexOptions.IgnoreCase)
                .IsMatch(messageLowercased);
        }

        public static void DetermineColorAndPrefix(LogType logType, out ConsoleColor? foregroundColor, out string prefix)
        {
            switch (logType)
            {
                case LogType.Command:
                case LogType.ExitCode:
                    foregroundColor = ConsoleColor.Magenta;
                    prefix = "[CMD]";
                    break;
                case LogType.Success:
                    foregroundColor = ConsoleColor.Green;
                    prefix = null;
                    break;
                case LogType.Info:
                    foregroundColor = ConsoleColor.Cyan;
                    prefix = null;
                    break;
                case LogType.Verbose:
                    foregroundColor = ConsoleColor.Magenta;
                    prefix = "[VERBOSE]";
                    break;
                case LogType.Warning:
                    foregroundColor = ConsoleColor.Yellow;
                    prefix = null;
                    break;
                case LogType.Error:
                    foregroundColor = ConsoleColor.Red;
                    prefix = null;
                    break;
                case LogType.Fatal:
                    foregroundColor = ConsoleColor.Red;
                    prefix = "[FATAL]";
                    break;
                default:
                    foregroundColor = null;
                    prefix = null;
                    break;
            }
        }

        public static void TryToExtractSonarProjectUrl(string line)
        {
            var sonarProjectUrlMatch = SonarProjectUrlRegex.Match(line);
            if (sonarProjectUrlMatch.Success)
            {
                SonarProjectUrl = sonarProjectUrlMatch.Groups["url"].Value;
            }
        }

        public static void SafeConsoleWriteLine(string line, ConsoleColor? foregroundColor = ConsoleColor.White, string formattedPrefix = "")
        {
            lock (SyncLock)
            {
                if (foregroundColor != null)
                {
                    Console.ForegroundColor = foregroundColor.Value;
                }

                Console.WriteLine(formattedPrefix + line);

                // In case user breaks/stops program, ensure colors are back to normal.
                // A try/finally in Main didn't always ensure color reset (ex: on a <ctrl-c>).
                Console.ResetColor();
            }
        }

        public static void DisplayQueuedMessages()
        {
            DetermineColorAndPrefix(LogType.Info, out var foregroundColor, out _);
            foreach (var message in LogHelper.MessageQueue.Distinct())
            {
                SafeConsoleWriteLine(message, foregroundColor);
            }
        }
    }
}
