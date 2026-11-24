using SuperFantasyKingdom;

namespace CaravanCommissions
{
    public static class CommissionCalendar
    {
        /// <summary>Read on the game thread. A pending order with no confirmed visit returns true and -1.</summary>
        public static bool TryGetPendingDelivery(out int visitDay)
        {
            visitDay = -1;
            var game = GameManager.Instance;
            if (!Plugin.Book.Pending || !game || !game.IsGameRunning() || game.IsUnloading() ||
                game.IsGameOver() || !Plugin.IsCurrentRun) return false;

            visitDay = Plugin.ConfirmedDeliveryDay();
            return true;
        }
    }
}
