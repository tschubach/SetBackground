using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine.Common;

namespace SetBackground
{
    internal static class ArgumentsHandler
    {
        public static UserArguments ReadUserArguments(string[] args, Action<LogType?, string[]> loggerCallback)
        {
            var userArguments = CreateDefaultUserArguments();
            var argsQueue = new Queue<string>(args);

            while (argsQueue.Count > 0)
            {
                var arg = argsQueue.Dequeue();

                if (arg.StartsWith("-") || arg.StartsWith("/"))
                {
                    IEnumerable<string> subArgs;
                    if (arg.StartsWith("--"))
                    {
                        // Examples: --help --verbose
                        subArgs = new[] { arg.Substring(2) };
                    }
                    else
                    {
                        // Examples: -? /? -v /v -cv
                        subArgs = arg.Substring(1).Select(c => c.ToString());
                    }
                    ReadSwitchArguments(loggerCallback, subArgs, userArguments, argsQueue);
                }
                else
                {
                    ReadNonSwitchArgument(loggerCallback, ref userArguments, arg);
                }
            }


            return userArguments;
        }

        private static UserArguments CreateDefaultUserArguments()
        {
            return new UserArguments
            {
                ImageFile = "",
                Style = Wallpaper.Style.Tile
            };
        }

        private static void ReadSwitchArguments(
            Action<LogType?, string[]> loggerCallback,
            IEnumerable<string> args,
            UserArguments userArguments,
            Queue<string> argsQueue
            )
        {
            foreach (var arg in args)
            {
                switch (arg)
                {
                    case "verbose":
                    case "v":
                        userArguments.Verbose = true;
                        break;
                    case "image":
                    case "i":
                        if (argsQueue.Count < 1)
                        {
                            UsageAndExit(loggerCallback, @"File switch wasn't provided a file name (ex: C:\gecko.bmp)");
                        }
                        userArguments.ImageFile = argsQueue.Dequeue();
                        break;
                    case "fit":
                    case "f":
                        if (argsQueue.Count < 1)
                        {
                            UsageAndExit(loggerCallback, "Visual Studio switch wasn't provided a version number (ex: 2017).");
                        }
                        var fit = argsQueue.Dequeue().ToLower();
                        userArguments.Style = fit switch
                        {
                            "fill" => Wallpaper.Style.Fill,
                            "fit" => Wallpaper.Style.Fit,
                            "stretch" => Wallpaper.Style.Stretch,
                            "center" => Wallpaper.Style.Center,
                            "span" => Wallpaper.Style.Span,
                            _ => Wallpaper.Style.Tile,
                        };
                        break;
                    case "help":
                    case "?":
                        UsageAndExit(loggerCallback);
                        break;
                    default:
                        UsageAndExit(loggerCallback, $"Unknown argument: {arg}");
                        break;
                }
            }
        }

        private static void ReadNonSwitchArgument(Action<LogType?, string[]> loggerCallback, ref UserArguments userArguments, string arg)
        {
            if (userArguments.ImageFile != null)
            {
                UsageAndExit(loggerCallback,
                    $"Only one image file argument is allowed. Two were provided: {userArguments.ImageFile}, {arg}");
            }

            if (!File.Exists(arg))
            {
                throw new FileNotFoundException($"File could not be found: {arg}");
            }

            userArguments.ImageFile = arg;
        }

        internal static void UsageAndExit(Action<LogType?, string[]> loggerCallback, string message = null)
        {
            if (message != null)
            {
                loggerCallback(LogType.Info, new[] { message });
            }

            loggerCallback(LogType.Info, new[] { "Usage:" });
            loggerCallback(LogType.Info, new[] { $"   {AppDomain.CurrentDomain.FriendlyName} [arguments]" });
            loggerCallback(LogType.Info, new[] { "Arguments:" });
            loggerCallback(LogType.Info, new[] { "   --image|-i       : Path to file to use as desktop background" });
            loggerCallback(LogType.Info, new[] { "   --fit|-f <style> : Option for fitting image to screen.  Style can be one of the following:" });
            loggerCallback(LogType.Info, new[] { "                         Fill    - The image is resized and cropped to fill the screen while maintaining the aspect ratio." });
            loggerCallback(LogType.Info, new[] { "                         Fit     - The image is resized to fit the screen while maintaining the aspect ratio." });
            loggerCallback(LogType.Info, new[] { "                         Stretch - The image is stretched to fill the screen" });
            loggerCallback(LogType.Info, new[] { "                         Tile    - The image is tiled across the screen" });
            loggerCallback(LogType.Info, new[] { "                         Center  - The image is centered in each screen" });
            loggerCallback(LogType.Info, new[] { "                         Span    - The image is resized and cropped to fill all screens while maintaining the aspect ratio." });
            loggerCallback(LogType.Info, new[] { "   --verbose|-v     : Verbose mode (for troubleshooting)" });
            loggerCallback(LogType.Info, new[] { "   -?|/?            : Prints this message" });
            Environment.Exit(0);
        }
    }
}
