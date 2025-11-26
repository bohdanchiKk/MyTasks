namespace TestServerApp
{
    public interface IConsole
    {
        ConsoleKeyInfo ReadKey(bool intercept);
        void WriteLine(string message);
    }

    public class SystemConsole : IConsole
    {
        public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);
        public void WriteLine(string message) => Console.WriteLine(message);
    }
}