using System.Collections.Generic;
using NUnit.Framework;

// Request1009Plan.md §89 — Heaven Of Bakery: 판마다 바뀌는 배치(MapLayout)·이름 바꾸기·주제 재료.
public class Request1123Tests
{
    [Test]
    public void Layout_SixDistinctSubMapOrders()
    {
        var seen = new HashSet<string>();
        foreach (int[] p in MapLayout.Permutations)
        {
            Assert.AreEqual(3, p.Length);
            var slots = new HashSet<int>(p);
            Assert.AreEqual(3, slots.Count, "each sub-map exactly once");
            foreach (int i in p) Assert.That(i, Is.InRange(0, 2));
            seen.Add(string.Join(",", p));
        }
        Assert.AreEqual(6, seen.Count, "3! orders");
    }

    [Test]
    public void Layout_RollAndDecodeStayInRange()
    {
        var rng = new System.Random(5);
        var orders = new HashSet<int>(); var lots = new HashSet<int>();
        for (int k = 0; k < 400; k++)
        {
            MapLayout.Layout l = MapLayout.Decode(MapLayout.Roll(rng));
            Assert.That(l.SubMapOrder, Is.InRange(0, MapLayout.Permutations.Length - 1));
            Assert.That(l.MaterialLot, Is.InRange(0, MapLayout.LotCount - 1));
            orders.Add(l.SubMapOrder); lots.Add(l.MaterialLot);
        }
        Assert.AreEqual(6, orders.Count, "all 6 orders appear");
        Assert.AreEqual(MapLayout.LotCount, lots.Count, "all lots appear");
        MapLayout.Layout bad = MapLayout.Decode(new[] { 99, -3 });
        Assert.AreEqual(5, bad.SubMapOrder); Assert.AreEqual(0, bad.MaterialLot);
        Assert.AreEqual(0, MapLayout.Decode(null).SubMapOrder);
    }

    [Test]
    public void MapLayoutKey_IsRoundScopedRoomKey()
    {
        CollectionAssert.Contains(NetKeys.RoundRoomKeys, NetKeys.MapLayout, "cleared when the round ends");
        Assert.IsTrue(NetKeys.IsMasterKey(NetKeys.MapLayout, NetKeys.Target.Room), "only the master writes it");
    }
}
