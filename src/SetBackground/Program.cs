namespace SetBackground
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var userArguments = ArgumentsHandler.ReadUserArguments(args, LogHelper.LoggerCallback);
            Wallpaper.Set(userArguments.ImageFile, userArguments.Style);
        }
    }
}