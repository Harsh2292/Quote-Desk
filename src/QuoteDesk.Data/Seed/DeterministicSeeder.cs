using Microsoft.EntityFrameworkCore;
using QuoteDesk.Data.Entities;
using QuoteDesk.Domain;

namespace QuoteDesk.Data.Seed;

/// <summary>
/// Fills an empty database with deterministic demo data — the same fixed seed produces the same
/// rows every time, so evals stay reproducible. Idempotent: if any customer already exists, this
/// is a no-op, so it is safe to call on every startup.
/// </summary>
public static class DeterministicSeeder
{
    /// <summary>Fixed so two runs against an empty database are byte-identical.</summary>
    public const int FixedSeed = 20260829;

    private static readonly TimeSpan Ist = TimeSpan.FromHours(5.5);

    public static async Task SeedAsync(QuoteDeskDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (await db.Customers.AnyAsync(cancellationToken))
        {
            return;
        }

        var random = new Random(FixedSeed);

        var customers = BuildCustomers();
        var shreeji = customers.Single(c => c.Name == "Shreeji Textiles");
        db.Customers.AddRange(customers);

        var catalogItems = BuildCatalogItems();
        db.CatalogItems.AddRange(catalogItems);
        db.StockLevels.AddRange(BuildStockLevels(catalogItems));
        db.PriceRules.AddRange(BuildPriceRules());

        // Customer.Id is DB-generated; OrderHistory and Enquiries below need the real values.
        await db.SaveChangesAsync(cancellationToken);

        db.OrderHistory.AddRange(BuildOrderHistory(random, customers, catalogItems, shreeji));
        db.Enquiries.AddRange(BuildEnquiries(customers, shreeji));

        await db.SaveChangesAsync(cancellationToken);
    }

    // ---- Customers -----------------------------------------------------------------------

    private static readonly string[] CompanyPrefixes =
    [
        "Jai", "Om", "Krishna", "Laxmi", "Ganesh", "Radhe", "Vishwakarma", "Bharat", "United",
        "National", "Surat", "Mahalaxmi", "Sanskar", "Rajlaxmi", "Vardhman", "Sunrise", "Silver Star",
        "Ambica", "Shivam", "Balaji", "Someshwar", "Ashapura", "Navkar", "Siddhi",
    ];

    private static readonly string[] CompanySuffixes =
        ["Textiles", "Mills", "Weaving Works", "Fabrics", "Spinning Mills", "Textile Industries", "Processors", "Synthetics"];

    private static readonly string[] ShipToCities = ["Surat", "Sachin", "Palsana", "Kadodara", "Pandesara"];

    private static List<Customer> BuildCustomers()
    {
        var customers = new List<Customer>
        {
            // The worked example in docs/DOMAIN.md.
            new()
            {
                Name = "Shreeji Textiles",
                EmailDomain = "shreejitextiles.com",
                WhatsAppNumber = "+91-98250-11223",
                Tier = CustomerTier.B,
                CreditDays = 45,
                GstIn = "24AAAAA0001A1Z5",
                DefaultShipTo = "Sachin",
            },
        };

        var index = 1;
        foreach (var prefix in CompanyPrefixes)
        {
            foreach (var suffix in CompanySuffixes)
            {
                if (customers.Count >= 25)
                {
                    return customers;
                }

                var name = $"{prefix} {suffix}";
                var tier = (CustomerTier)(index % 3);
                index++;

                customers.Add(new Customer
                {
                    Name = name,
                    EmailDomain = index % 4 == 0 ? null : $"{Slug(name)}.com",
                    WhatsAppNumber = index % 3 == 0 ? $"+91-9{800000000 + index * 137 % 99999999:D8}" : null,
                    Tier = tier,
                    CreditDays = tier switch { CustomerTier.A => 60, CustomerTier.B => 45, _ => 30 },
                    GstIn = $"24AAAAA{index:D4}A1Z5",
                    DefaultShipTo = ShipToCities[index % ShipToCities.Length],
                });
            }
        }

        return customers;
    }

    private static string Slug(string name) => name.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    // ---- Catalog items ---------------------------------------------------------------------

    private static readonly int[] BearingBoreCodes =
        [6200, 6201, 6202, 6203, 6204, 6205, 6206, 6207, 6208, 6209, 6210, 6211, 6212, 6213, 6214, 6215, 6216, 6217, 6218, 6219];

    private static readonly string[] BearingSuffixes = ["2RS", "ZZ", "RS", "2Z"];

    private static readonly int[] BeltWidthsMm = [10, 13, 16, 20, 25, 32, 40, 50, 63, 75];

    private static readonly (string Name, string Code)[] BeltTypes =
    [
        ("PU Timing Belt", "PU"),
        ("Rubber V-Belt", "VBLT"),
        ("Flat Belt", "FLAT"),
        ("Rubber Timing Belt", "RTB"),
        ("Cogged V-Belt", "CVB"),
    ];

    private static readonly (string Name, string Code)[] SpindleTapeApplications =
        [("Ring Frame", "RF"), ("Simplex", "SPX"), ("Doubling Frame", "DF"), ("Roving Frame", "RVF")];

    private static readonly string[] SpindleTapeThicknesses = ["4mm", "5mm", "6mm", "7mm", "8mm", "9mm", "10mm", "11mm"];

    private static readonly decimal[] GearModules = [1m, 1.5m, 2m, 2.5m, 3m, 3.5m, 4m, 5m, 6m, 8m];

    private static readonly int[] GearTeeth = [18, 20, 24, 28, 30, 36, 40, 44, 48, 54];

    private static List<CatalogItem> BuildCatalogItems()
    {
        var items = new List<CatalogItem>();

        foreach (var code in BearingBoreCodes)
        {
            foreach (var suffix in BearingSuffixes)
            {
                // Fits the worked example exactly: at code 6203 this formula gives list 250.00 /
                // cost 197.80 — an 8% discount lands at a 14% margin, as docs/DOMAIN.md asserts.
                var listPrice = Money.Round(100m + ((code - 6200) * 50m));
                items.Add(new CatalogItem
                {
                    Sku = $"BRG-{code}-{suffix}",
                    Name = $"{code} Series Ball Bearing ({suffix})",
                    Category = "Bearings",
                    Uom = "Nos",
                    ListPrice = listPrice,
                    CostPrice = Money.Round(listPrice * 0.7912m),
                });
            }
        }

        foreach (var width in BeltWidthsMm)
        {
            foreach (var (typeName, typeCode) in BeltTypes)
            {
                var listPrice = Money.Round(15m + (width * 0.6m));
                items.Add(new CatalogItem
                {
                    Sku = $"BELT-{typeCode}-{width}MM",
                    Name = $"{width}mm {typeName}",
                    Category = "Belts",
                    Uom = "Mtr",
                    ListPrice = listPrice,
                    CostPrice = Money.Round(listPrice * 0.75m),
                });
            }
        }

        foreach (var (appName, appCode) in SpindleTapeApplications)
        {
            foreach (var thickness in SpindleTapeThicknesses)
            {
                var listPrice = Money.Round(20m + (SpindleTapeThicknesses.ToList().IndexOf(thickness) * 3m));
                items.Add(new CatalogItem
                {
                    Sku = $"SPT-{appCode}-{thickness.ToUpperInvariant()}",
                    // Deliberately identical name across thicknesses for the same application — the
                    // 6mm/8mm pair is distinguished only by Attributes, per docs/DOMAIN.md.
                    Name = $"{appName} Spindle Tape",
                    Category = "SpindleTapes",
                    Uom = "Mtr",
                    ListPrice = listPrice,
                    CostPrice = Money.Round(listPrice * 0.70m),
                    Attributes = thickness,
                });
            }
        }

        foreach (var module in GearModules)
        {
            foreach (var teeth in GearTeeth)
            {
                var listPrice = Money.Round(40m + (module * 20m) + (teeth * 0.5m));
                items.Add(new CatalogItem
                {
                    Sku = $"GEAR-M{module}-{teeth}T",
                    Name = $"Module {module} Spur Gear ({teeth}T)",
                    Category = "Gears",
                    Uom = "Nos",
                    ListPrice = listPrice,
                    CostPrice = Money.Round(listPrice * 0.75m),
                });
            }
        }

        // The deliberate margin-floor case: a 10% list-to-cost spread means any customer's usual
        // slab + tier discount pushes the net margin below the 10% floor.
        var marginCase = items.Single(i => i.Sku == "GEAR-M2-40T");
        marginCase.ListPrice = 100.00m;
        marginCase.CostPrice = 90.00m;

        return items;
    }

    private static List<StockLevel> BuildStockLevels(List<CatalogItem> items)
    {
        var levels = new List<StockLevel>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var leadTimeDays = item.Category switch
            {
                "Bearings" => 5,
                "Belts" => 9,
                "SpindleTapes" => 7,
                _ => 6,
            };
            var onHand = 50 + ((i * 7) % 400);

            levels.Add(new StockLevel
            {
                Sku = item.Sku,
                OnHand = onHand,
                LeadTimeDays = leadTimeDays,
                ReorderLevel = onHand / 5,
            });
        }

        // The deliberate short-stock case: 12 on hand against a typical 40-unit ask.
        levels.Single(s => s.Sku == "BELT-PU-25MM").OnHand = 12;

        // Comfortably covers the worked example's 250-unit bearing order.
        levels.Single(s => s.Sku == "BRG-6203-2RS").OnHand = 500;

        return levels;
    }

    private static List<PriceRule> BuildPriceRules()
    {
        // One slab ladder per category, matching SlabDiscountPolicy.DefaultLadder exactly so
        // price_quote reproduces the worked example end to end. (docs/SPEC.md estimated ~40 rows;
        // 16 is enough here — a real per-SKU override table is easy to grow later without a schema
        // change, and the demo does not need it.)
        var categories = new[] { "Bearings", "Belts", "SpindleTapes", "Gears" };
        var rules = new List<PriceRule>();

        foreach (var category in categories)
        {
            foreach (var slab in SlabDiscountPolicy.DefaultLadder)
            {
                rules.Add(new PriceRule { Scope = "Category", Target = category, MinQty = slab.MinQty, DiscountPct = slab.DiscountPct });
            }
        }

        return rules;
    }

    // ---- Order history ---------------------------------------------------------------------

    private static readonly DateTimeOffset OrderHistoryAnchor = new(2026, 3, 1, 0, 0, 0, Ist);

    private static List<OrderHistory> BuildOrderHistory(
        Random random, List<Customer> customers, List<CatalogItem> catalogItems, Customer shreeji)
    {
        const int ordersPerCustomer = 48;
        var orders = new List<OrderHistory>();

        foreach (var customer in customers)
        {
            var remaining = ordersPerCustomer;

            // The deliberate "same as last time" case: three prior purchases of the 2RS variant,
            // the most recent at the 8% rate Kiran expects to be honoured again.
            if (customer == shreeji)
            {
                orders.Add(new OrderHistory { CustomerId = shreeji.Id, Sku = "BRG-6203-2RS", Qty = 200, UnitPrice = 230.00m, OrderedAt = new DateTimeOffset(2025, 11, 10, 10, 0, 0, Ist) });
                orders.Add(new OrderHistory { CustomerId = shreeji.Id, Sku = "BRG-6203-2RS", Qty = 180, UnitPrice = 230.00m, OrderedAt = new DateTimeOffset(2026, 1, 5, 10, 0, 0, Ist) });
                orders.Add(new OrderHistory { CustomerId = shreeji.Id, Sku = "BRG-6203-2RS", Qty = 220, UnitPrice = 230.00m, OrderedAt = new DateTimeOffset(2026, 2, 20, 10, 0, 0, Ist) });
                remaining -= 3;
            }

            for (var i = 0; i < remaining; i++)
            {
                var item = catalogItems[random.Next(catalogItems.Count)];
                var qty = random.Next(1, 51);
                var discount = random.Next(0, 11) / 100m;
                var unitPrice = Money.Round(item.ListPrice * (1 - discount));
                var orderedAt = OrderHistoryAnchor.AddDays(-random.Next(1, 730));

                orders.Add(new OrderHistory
                {
                    CustomerId = customer.Id,
                    Sku = item.Sku,
                    Qty = qty,
                    UnitPrice = unitPrice,
                    OrderedAt = orderedAt,
                });
            }
        }

        return orders;
    }

    // ---- Enquiries --------------------------------------------------------------------------

    private static List<Enquiry> BuildEnquiries(List<Customer> customers, Customer shreeji)
    {
        const string shreejiEnquiryBody = """
            Hi Mehul bhai,
            Need urgent quote —
            250 nos of the 6203 bearings (same as last time)
            40 mtr of the 25mm PU timing belt
            12 pcs ring frame spindle tape, the thicker one

            Delivery at our Sachin unit, need by 5th. Last time you gave 8% on bearings, please keep same.

            Kiran — Shreeji Textiles
            """;

        var customerByIndex = customers.Skip(1).ToList(); // index 0 is Shreeji, already handled

        return
        [
            new Enquiry
            {
                Channel = "Email",
                SenderId = "kiran@shreejitextiles.com",
                RawBody = shreejiEnquiryBody,
                ReceivedAt = new DateTimeOffset(2026, 3, 26, 8, 41, 0, Ist),
                CustomerId = shreeji.Id,
                Status = "pending",
            },
            new Enquiry
            {
                // The deliberate unknown-sender case: matches no customer record.
                Channel = "WhatsApp",
                SenderId = "+91-90000-00000",
                RawBody = "Need 50 pcs bearing 6203 asap, whats the rate",
                ReceivedAt = new DateTimeOffset(2026, 3, 15, 11, 20, 0, Ist),
                CustomerId = null,
                Status = "new_customer",
            },
            new Enquiry
            {
                Channel = "Paste",
                SenderId = $"{Slug(customerByIndex[0].Name)}.com",
                RawBody = "Require 100 pcs module 2 spur gear 40T, please quote with delivery.",
                ReceivedAt = new DateTimeOffset(2026, 1, 12, 9, 15, 0, Ist),
                CustomerId = customerByIndex[0].Id,
                Status = "pending",
            },
            new Enquiry
            {
                Channel = "WhatsApp",
                SenderId = customerByIndex[1].WhatsAppNumber ?? "+91-90000-00001",
                RawBody = "20mm PU belt 15 mtr chahiye, kal tak mil jayega?",
                ReceivedAt = new DateTimeOffset(2026, 1, 20, 16, 5, 0, Ist),
                CustomerId = customerByIndex[1].Id,
                Status = "pending",
            },
            new Enquiry
            {
                Channel = "Email",
                SenderId = $"purchase@{Slug(customerByIndex[2].Name)}.com",
                RawBody = "Please send quotation for 300 nos 6205 2RS bearing, our usual terms.",
                ReceivedAt = new DateTimeOffset(2026, 2, 2, 10, 30, 0, Ist),
                CustomerId = customerByIndex[2].Id,
                Status = "quoted",
            },
            new Enquiry
            {
                Channel = "Paste",
                SenderId = $"{Slug(customerByIndex[3].Name)}.com",
                RawBody = "Doubling frame spindle tape 8mm, 25 pcs, urgent.",
                ReceivedAt = new DateTimeOffset(2026, 2, 8, 14, 45, 0, Ist),
                CustomerId = customerByIndex[3].Id,
                Status = "pending",
            },
            new Enquiry
            {
                Channel = "WhatsApp",
                SenderId = customerByIndex[4].WhatsAppNumber ?? "+91-90000-00002",
                RawBody = "6210 ZZ bearing 60 pcs ka rate bhejo",
                ReceivedAt = new DateTimeOffset(2026, 2, 14, 12, 0, 0, Ist),
                CustomerId = customerByIndex[4].Id,
                Status = "pending",
            },
            new Enquiry
            {
                Channel = "Email",
                SenderId = $"stores@{Slug(customerByIndex[5].Name)}.com",
                RawBody = "Need 500 nos 6200 series bearing (2RS) for our next production run.",
                ReceivedAt = new DateTimeOffset(2026, 2, 18, 9, 0, 0, Ist),
                CustomerId = customerByIndex[5].Id,
                Status = "quoted",
            },
            new Enquiry
            {
                Channel = "Paste",
                SenderId = $"{Slug(customerByIndex[6].Name)}.com",
                RawBody = "Roving frame tape 9mm x 30 mtr required, share price and lead time.",
                ReceivedAt = new DateTimeOffset(2026, 3, 1, 15, 20, 0, Ist),
                CustomerId = customerByIndex[6].Id,
                Status = "pending",
            },
            new Enquiry
            {
                Channel = "WhatsApp",
                SenderId = customerByIndex[7].WhatsAppNumber ?? "+91-90000-00003",
                RawBody = "Cogged v belt 40mm 10 mtr, price bhejo",
                ReceivedAt = new DateTimeOffset(2026, 3, 8, 17, 40, 0, Ist),
                CustomerId = customerByIndex[7].Id,
                Status = "pending",
            },
            new Enquiry
            {
                Channel = "Email",
                SenderId = $"purchase@{Slug(customerByIndex[8].Name)}.com",
                RawBody = "Quotation needed: module 3 spur gear 36T x 40 nos.",
                ReceivedAt = new DateTimeOffset(2026, 3, 20, 11, 10, 0, Ist),
                CustomerId = customerByIndex[8].Id,
                Status = "pending",
            },
            new Enquiry
            {
                Channel = "Paste",
                SenderId = $"{Slug(customerByIndex[9].Name)}.com",
                RawBody = "Flat belt 32mm, 20 mtr required for the weaving section.",
                ReceivedAt = new DateTimeOffset(2026, 3, 24, 13, 30, 0, Ist),
                CustomerId = customerByIndex[9].Id,
                Status = "pending",
            },
        ];
    }
}
