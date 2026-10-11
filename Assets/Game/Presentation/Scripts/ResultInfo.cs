namespace FunFlow.Presentation
{
    /// <summary>Итог попытки для экрана результата. Заполняет Core (или Sandbox-драйвер).</summary>
    public readonly struct ResultInfo
    {
        public readonly bool Won;
        public readonly int Value;
        /// <summary>Сколько секунд оставалось на таймере; меньше 0 — тревога не включалась.</summary>
        public readonly float AlarmSecondsLeft;
        /// <summary>Необязательная статистика; меньше 0 — неизвестно, строка не показывается.</summary>
        public readonly int Launches;
        public readonly int Items;
        public readonly int Cuts;

        public ResultInfo(bool won, int value, float alarmSecondsLeft = -1f, int launches = -1, int items = -1, int cuts = -1)
        {
            Won = won;
            Value = value < 0 ? 0 : value;
            AlarmSecondsLeft = alarmSecondsLeft;
            Launches = launches;
            Items = items;
            Cuts = cuts;
        }

        public bool AlarmTriggered => AlarmSecondsLeft >= 0f;
        public bool HasStats => Launches >= 0 && Items >= 0 && Cuts >= 0;
    }
}
