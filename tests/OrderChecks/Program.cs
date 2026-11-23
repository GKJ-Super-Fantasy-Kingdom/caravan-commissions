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
Console.WriteLine($"{passed} order checks passed.");
