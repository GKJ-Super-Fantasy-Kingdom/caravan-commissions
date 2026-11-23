using System;
using System.Collections.Generic;
using System.Linq;

namespace CaravanCommissions
{
    [Serializable]
    internal sealed class OrderBook
    {
        public int day = -1;
        public int lastVisit, dawnVisit;
        public List<int> spent = new List<int>();
        public string[] items = Array.Empty<string>();
        public string pool;

        internal bool Pending => items != null && items.Length == 3;

        internal void BeginDay(int current)
        {
            if (day == current) return;
            day = current;
            lastVisit = dawnVisit = 0;
            spent.Clear();
        }

        internal int StartVisit(int current, bool dawn)
        {
            BeginDay(current);
            // Older checkpoints recorded spent ordinals only. Their first ordinary
            // dawn replay must stay spent, but a new invitation needs a fresh ordinal.
            if (lastVisit == 0 && spent.Count > 0)
            {
                lastVisit = spent.Max();
                if (spent.Contains(1)) dawnVisit = 1;
            }
            if (dawn && dawnVisit > 0) return dawnVisit;
            int number = ++lastVisit;
            if (dawn) dawnVisit = number;
            return number;
        }

        internal bool Available(int current, int visit) =>
            current == day && visit > 0 && !spent.Contains(visit);

        [NonSerialized] private bool inTransaction;
        private static bool Valid(string[] ids) => ids != null && ids.Length == 3 &&
            !ids.Any(string.IsNullOrEmpty) && ids.Distinct().Count() == 3;

        internal bool Exchange(int current, int visit, string[] expected, string kingdom, string[] replacement, Func<string, string> identify)
        {
            if (!Pending || inTransaction || items != expected || !Available(current, visit) ||
                items.All(id => OfferIds.Available(id, identify)) || !Valid(replacement) ||
                !replacement.All(id => OfferIds.Available(id, identify))) return false;
            items = (string[])replacement.Clone();
            pool = kingdom;
            spent.Add(visit);
            return true;
        }

        internal bool Claim(int current, int visit, string[] expected, string id,
            Func<string, string> identify, Func<string, bool> grant)
        {
            if (inTransaction || !Pending || items != expected || !Available(current, visit) ||
                !items.Contains(id) || !OfferIds.Available(id, identify)) return false;
            inTransaction = true;
            try
            {
                if (!grant(id)) return false;
                Settle(visit, true);
                return true;
            }
            finally { inTransaction = false; }
        }

        internal bool Place(int current, int visit, string kingdom, string[] promised, Func<bool> pay)
        {
            if (inTransaction || Pending || !Available(current, visit) || !Valid(promised)) return false;
            var snapshot = (string[])promised.Clone();
            inTransaction = true;
            try
            {
                if (!pay()) return false;
                spent.Add(visit);
                pool = kingdom;
                items = snapshot;
                return true;
            }
            finally { inTransaction = false; }
        }

        internal bool Finish(int current, int visit, bool collect)
        {
            if (inTransaction || !Available(current, visit)) return false;
            Settle(visit, collect);
            return true;
        }

        private void Settle(int visit, bool collect)
        {
            spent.Add(visit);
            if (collect)
            {
                items = Array.Empty<string>();
                pool = null;
            }
        }
    }
}
