namespace TransactTcp
{
    public class ConnectionListenerSettings
    {
        public ConnectionListenerSettings(
            int backLog = 0,
            ConnectionSettings newConnectionSettings = null)
        {
            BackLog = backLog;
            NewConnectionSettings = newConnectionSettings;
        }

        public int BackLog { get; }
        public ConnectionSettings NewConnectionSettings { get; }
    }
}