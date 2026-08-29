namespace QuoteDesk.Domain;

/// <summary>Flat freight zone, by destination.</summary>
public enum FreightZone
{
    Local,
    Regional,
    National,
}

/// <summary>A zone's flat fee and the transit days from dispatch to the customer's door.</summary>
public sealed record FreightZoneConfig(decimal FlatFee, int TransitDays);

/// <summary>Flat freight by destination zone, waived above an order value threshold.</summary>
public static class FreightPolicy
{
    public static readonly IReadOnlyDictionary<FreightZone, FreightZoneConfig> Zones =
        new Dictionary<FreightZone, FreightZoneConfig>
        {
            [FreightZone.Local] = new FreightZoneConfig(0m, 1),
            [FreightZone.Regional] = new FreightZoneConfig(450m, 3),
            [FreightZone.National] = new FreightZoneConfig(1_200m, 5),
        };

    /// <summary>Freight is waived once the taxable value exceeds this. Exactly at the threshold, freight still applies.</summary>
    public const decimal WaiverThreshold = 50_000m;

    public static decimal ResolveFreight(FreightZone zone, decimal taxableValue) =>
        taxableValue > WaiverThreshold ? 0m : Zones[zone].FlatFee;

    public static int ResolveTransitDays(FreightZone zone) => Zones[zone].TransitDays;
}
