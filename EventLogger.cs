using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.State;
using System;

namespace PoGo.NecroBot.GUI
{
    public delegate void Write(string message, LogLevel level, ConsoleColor color);

    public class EventLogger : ILogger
    {
        private readonly LogLevel _maxLogLevel;
        private readonly Write _writer;
        private ISession _session;

        public EventLogger(LogLevel maxLogLevel, Write writer)
        {
            _maxLogLevel = maxLogLevel;
            _writer = writer;
        }

        public void Write(string message, LogLevel level = LogLevel.Info, ConsoleColor color = ConsoleColor.Black)
        {
            if (level == LogLevel.Debug)
            {
                return;
            }
            _writer(message, level, color);

        }

        public void SetSession(ISession session)
        {
            _session = session;
        }

        public void lineSelect(int lineChar = 0, int linesUp = 1)
        {
            throw new NotImplementedException();
        }
    }
}
