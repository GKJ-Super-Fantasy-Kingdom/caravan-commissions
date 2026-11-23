using System;
using System.Linq;
using System.Text.Json;
using CaravanCommissions;

var options = new JsonSerializerOptions { IncludeFields = true };
OrderBook Copy(OrderBook book) => JsonSerializer.Deserialize<OrderBook>(JsonSerializer.Serialize(book, options), options);
int passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS " + name);
    passed++;
}

foreach (var next in new[] { 16, 17, 23 })
{
    var book = new OrderBook();
    book.BeginDay(16);
    var earlierSave = Copy(book);
    int material = 6;
    bool Pay() { material -= 3; return true; }
    var promised = new[] { "A", "B", "C" };
    Check("shortfall leaves visit open", !book.Place(16, 1, "Pool", promised, () => false) && book.Available(16, 1));
    Check("invalid item pool never debits", !book.Place(16, 1, "Pool", new[] { "A", "A", "C" }, Pay) && material == 6);
    Check("confirm charges once", book.Place(16, 1, "Pool", promised, Pay) &&
        !book.Place(16, 1, "Pool", promised, Pay) && material == 3);
    promised[0] = "Changed";
    Check("frozen offer survives reload", Copy(book).items.SequenceEqual(new[] { "A", "B", "C" }));
    Check("rollback before order restores offer", !earlierSave.Pending && earlierSave.Available(16, 1));
    book = Copy(book);
    Check("ordering visit stays spent", !book.Available(16, 1));
    book.BeginDay(next);
    int delivery = next == 16 ? 2 : 1;
    Check("later visit eligible", book.Available(next, delivery));
    Check("pending order blocks a second charge", !book.Place(next, delivery, "Other", promised, Pay) && material == 3);
    Check("defer keeps order and consumes visit", book.Finish(next, delivery, false) && book.Pending && !book.Available(next, delivery));
    var deferred = Copy(book);
    book.BeginDay(next + 4);
    Check("claim settles once", book.Finish(next + 4, 1, true) && !book.Finish(next + 4, 1, true));
    book = Copy(book);
    Check("settled checkpoint cannot double claim", !book.Pending && !book.Available(next + 4, 1));
    Check("backup keeps earlier pending order", deferred.Pending && deferred.pool == "Pool");
}
Check("new run starts empty", !new OrderBook().Pending);
var catalog = new System.Collections.Generic.Dictionary<string, string> {
    ["alias"] = "mod.B", ["A"] = "A", ["mod.B"] = "mod.B", ["C"] = "C", ["D"] = "D", ["E"] = "E",
    ["broken"] = "unregistered"
};
string Identify(string id) => catalog.TryGetValue(id, out var canonical) ? canonical : null;
var ids = OfferIds.FromPool(new[] { null, "A", "alias", "mod.B", "broken", "C", "D", "E" }, Identify);
Check("resolve aliases and exclude broken registrations", ids.SequenceEqual(new[] { "A", "mod.B", "C", "D", "E" }));
var pages = OfferIds.Pages(ids).ToArray();
Check("appended custom items can be previewed", pages.Length == 2 && pages.SelectMany(x => x).Distinct().SequenceEqual(ids));
Check("every page contains three distinct actual items", pages.All(x => x.Length == 3 && x.Distinct().Count() == 3));
Check("short pools cannot hang", OfferIds.Draw(new[] { "A" }, _ => 0).SequenceEqual(new[] { "A" }));
Check("empty pools never ask the RNG", OfferIds.Draw(Array.Empty<string>(), _ => throw new Exception()).Length == 0);
Check("stuck modded RNG terminates", OfferIds.Draw(ids, _ => 0).Distinct().Count() == 3);
var once = new OrderBook();
once.BeginDay(16);
Check("reentrant payment blocked", once.Place(16, 1, "Original", pages[0],
    () => !once.Place(16, 1, "Duplicate", pages[0], () => throw new Exception())));
var snapshot = once.items;
once.BeginDay(17);
catalog["A"] = "D";
Check("rebound key cannot deliver the wrong identity", !once.Claim(17, 1, snapshot, "A", Identify, _ => throw new Exception()));
Check("replacement requires a complete resolvable preview", !once.Exchange(17, 1, snapshot, "Invalid", pages[0], Identify));
Check("remaining item can settle a partial order once", once.Claim(17, 1, snapshot, "C", Identify, id =>
{
    Check("reentrant grant blocked", !once.Claim(17, 1, snapshot, id, Identify, _ => throw new Exception()));
    Check("defer during grant blocked", !once.Finish(17, 1, false));
    return true;
}));
var source = new[] { "A", "A", "A", "B", "C", "D" };
var weighted = OfferIds.Weighted(source, id => id);
Check("first ordinary draw retains all weighted positions", Enumerable.Range(0, source.Length).All(slot =>
{
    int n = slot;
    var draw = OfferIds.Draw(weighted, max => n++ % max);
    return draw.Length == 3 && draw.Distinct().Count() == 3 && draw[0] == source[slot];
}));
var visits = new OrderBook();
int dawn = visits.StartVisit(16, true);
Check("order at the dawn visit", visits.Place(16, dawn, "Pool", source.Distinct().Take(3).ToArray(), () => true));
visits = Copy(visits);
Check("new invitation after resume is available without replay", visits.Available(16, visits.StartVisit(16, false)));
Check("replayed dawn remains spent after another invitation", !visits.Available(16, visits.StartVisit(16, true)));
Check("next dawn can deliver", visits.Available(17, visits.StartVisit(17, true)) && visits.Pending);
var legacy = new OrderBook { day = 16, spent = new() { 1, 3 } };
Check("legacy spent ordinals migrate without blocking a new invitation",
    legacy.StartVisit(16, false) == 4 && legacy.StartVisit(16, true) == 1);
Console.WriteLine($"{passed} order checks passed.");
