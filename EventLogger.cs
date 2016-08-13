using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.State;
using System;
using System.Drawing;

namespace PoGo.NecroBot.GUI
{
    public delegate void Write(string message, LogLevel level, Color color);

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

        public void Write(string message, LogLevel level = LogLevel.Info, ConsoleColor consoleColor = ConsoleColor.Black)
        {
            Color color = Color.White;
            if (level == LogLevel.Debug)
            {
                return;
            }

            switch (level)
            {
                case LogLevel.None:
                    color = Color.Gray;
                    break;
                case LogLevel.Error:
                    color = Color.Red;
                    break;
                case LogLevel.Warning:
                    color = Color.Orange;
                    break;
                case LogLevel.Pokestop:
                    break;
                case LogLevel.Farming:
                    break;
                case LogLevel.Sniper:
                    color = Color.LightYellow;
                    break;
                case LogLevel.Recycling:
                    color = Color.Gray;
                    break;
                case LogLevel.Berry:
                    color = Color.MediumVioletRed;
                    break;
                case LogLevel.Caught:
                    color = Color.Green;
                    break;
                case LogLevel.Flee:
                    color = Color.DarkRed;
                    break;
                case LogLevel.Transfer:
                    break;
                case LogLevel.Evolve:
                    break;
                case LogLevel.Egg:
                case LogLevel.Update:
                    color = Color.LightSlateGray;
                    break;
                case LogLevel.Info:
                    color = Color.LightSkyBlue;
                    break;
                case LogLevel.New:
                    break;
                case LogLevel.SoftBan:
                    color = Color.OrangeRed;
                    break;
                case LogLevel.LevelUp:
                    color = Color.LightGreen;
                    break;
                case LogLevel.Debug:
                    color = Color.LightGray;
                    break;
                default:
                    color = Color.White;
                    break;
            }

            _writer(message, level, color);
        }

        public void Write(string message, LogLevel level, Color color)
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
