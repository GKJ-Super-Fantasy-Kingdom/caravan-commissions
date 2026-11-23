using System;
using System.Collections.Generic;
using System.Linq;

namespace CaravanCommissions
{
    [Serializable]
    internal sealed class OrderBook
    {
        public int day = -1;
        public List<int> spent = new List<int>();
        public string[] items = Array.Empty<string>();
        public string pool;

        internal bool Pending => items != null && items.Length == 3;

        internal void BeginDay(int current)
        {
            if (day == current) return;
            day = current;
            spent.Clear();
        }

        internal bool Available(int current, int visit) =>
            current == day && visit > 0 && !spent.Contains(visit);

        internal bool Place(int current, int visit, string kingdom, string[] promised, Func<bool> pay)
        {
            if (Pending || !Available(current, visit) || promised == null ||
                promised.Length != 3 || promised.Any(string.IsNullOrEmpty) ||
                promised.Distinct().Count() != 3) return false;
            var snapshot = (string[])promised.Clone();
            if (!pay()) return false;
            spent.Add(visit);
            pool = kingdom;
            items = snapshot;
            return true;
        }

        internal bool Finish(int current, int visit, bool collect)
        {
            if (!Available(current, visit)) return false;
            spent.Add(visit);
            if (collect)
            {
                items = Array.Empty<string>();
                pool = null;
            }
            return true;
        }
    }
}
