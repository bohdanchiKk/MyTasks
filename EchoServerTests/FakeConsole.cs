using System.Collections.Concurrent;
using System;

namespace TestServerApp
{
    public class FakeConsole : IConsole
    {
        private readonly ConcurrentQueue<ConsoleKeyInfo> _keys = new();
        public void Enqueue(ConsoleKey key)
        {
            _keys.Enqueue(new ConsoleKeyInfo((char)key, key, false, false, false));
        }
        public ConsoleKeyInfo ReadKey(bool intercept)
        {
            while (true)
            {
                if (_keys.TryDequeue(out var k)) return k;
                Thread.Sleep(10);
            }
        }
        public void WriteLine(string message) { }
    }
}