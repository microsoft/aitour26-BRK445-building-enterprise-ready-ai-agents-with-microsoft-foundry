using SharedEntities;

namespace ZavaDatabaseInitialization;

public static class DbInitializer
{
    public static void Initialize(Context context)
    {
        // Seed Products
        if (!context.Product.Any())
        {
            var products = new List<Product>
            {
                new Product { Name = "Interior Wall Paint - White Matte", Description = "Premium interior latex paint with smooth matte finish, low VOC.", Price = 29.99m, ImageUrl = "paint_white_1.png" },
                new Product { Name = "Exterior Wood Stain - Cedar", Description = "Weather-resistant wood stain for decks and siding with UV protection.", Price = 34.99m, ImageUrl = "wood_stain_cedar.png" },
                new Product { Name = "Cordless Drill Kit", Description = "18V cordless drill with two batteries, charger, and 25-piece bit set.", Price = 79.99m, ImageUrl = "cordless_drill_18v.png" },
                new Product { Name = "Circular Saw - 7 1/4\"", Description = "Powerful circular saw for precise cuts in plywood and dimensional lumber.", Price = 119.99m, ImageUrl = "circular_saw_7_14.png" },
                new Product { Name = "Plywood Sheet - 3/4 inch", Description = "High-quality furniture-grade plywood sheet, 4x8 ft, versatile for cabinetry and shelving.", Price = 49.99m, ImageUrl = "plywood_3_4_4x8.png" },
                new Product { Name = "Pressure-Treated Lumber - 2x4", Description = "2x4 pressure-treated lumber, suitable for outdoor framing and decks.", Price = 6.49m, ImageUrl = "lumber_2x4.png" },
                new Product { Name = "Painter's Roller Kit", Description = "Complete roller kit with roller covers, tray, and extension pole for smooth wall coverage.", Price = 19.99m, ImageUrl = "painters_roller_kit.png" },
                new Product { Name = "Finish Nails - Box 1000", Description = "1 1/4 inch finish nails for trim and finishing work.", Price = 7.99m, ImageUrl = "finish_nails_box.png" },
                new Product { Name = "Wood Glue - 16 oz", Description = "High-strength PVA wood glue for furniture and cabinetry projects.", Price = 6.99m, ImageUrl = "wood_glue_16oz.png" },
                new Product { Name = "Sandpaper Assortment", Description = "Assorted grit sandpaper pack (80-400 grit) for rough and fine sanding.", Price = 9.99m, ImageUrl = "sandpaper_assortment.png" },
                new Product { Name = "Stud Finder", Description = "Electronic stud finder for locating studs, live wires, and edges behind walls.", Price = 24.99m, ImageUrl = "stud_finder.png" },
                new Product { Name = "Caulking Gun + Silicone", Description = "Smooth-action caulking gun with a tube of silicone sealant for gaps and joints.", Price = 12.99m, ImageUrl = "caulking_gun_silicone.png" },
                new Product { Name = "Toolbox - Metal", Description = "Durable metal toolbox with removable tray for organising hand tools.", Price = 39.99m, ImageUrl = "metal_toolbox.png" },
                new Product { Name = "Tape Measure - 25ft", Description = "25-foot tape measure with locking mechanism and belt clip.", Price = 9.49m, ImageUrl = "tape_measure_25ft.png" },
                new Product { Name = "Protective Safety Glasses", Description = "ANSI-rated safety glasses with anti-fog coating for eye protection.", Price = 6.49m, ImageUrl = "safety_glasses.png" },
            };
            context.AddRange(products);
        }

        // Seed Customers
        if (!context.Customer.Any())
        {
            var customers = new List<CustomerInformation>
            {
                new CustomerInformation { Id = "1", Name = "John Smith", OwnedTools = new[] { "hammer", "screwdriver", "measuring tape" }, Skills = new[] { "basic DIY", "painting" } },
                new CustomerInformation { Id = "2", Name = "Sarah Johnson", OwnedTools = new[] { "drill", "saw", "level", "hammer" }, Skills = new[] { "intermediate DIY", "woodworking", "tiling" } },
                new CustomerInformation { Id = "3", Name = "Mike Davis", OwnedTools = new[] { "basic toolkit" }, Skills = new[] { "beginner DIY" } }
            };
            context.AddRange(customers);
        }

        // Seed Tools (Inventory)
        if (!context.Tool.Any())
        {
            var tools = new List<ToolRecommendation>
            {
                new ToolRecommendation { Name = "Paint Roller", Sku = "PAINT-ROLLER-9IN", IsAvailable = true, Price = 12.99m, Description = "9-inch paint roller for smooth walls" },
                new ToolRecommendation { Name = "Paint Brush Set", Sku = "BRUSH-SET-3PC", IsAvailable = true, Price = 24.99m, Description = "3-piece brush set for detail work" },
                new ToolRecommendation { Name = "Drop Cloth", Sku = "DROP-CLOTH-9X12", IsAvailable = true, Price = 8.99m, Description = "Plastic drop cloth protection" },
                new ToolRecommendation { Name = "Circular Saw", Sku = "SAW-CIRCULAR-7IN", IsAvailable = true, Price = 89.99m, Description = "7.25-inch circular saw for wood cutting" },
                new ToolRecommendation { Name = "Wood Stain", Sku = "STAIN-WOOD-QT", IsAvailable = false, Price = 15.99m, Description = "1-quart wood stain in natural color" },
                new ToolRecommendation { Name = "Safety Glasses", Sku = "SAFETY-GLASSES", IsAvailable = true, Price = 5.99m, Description = "Safety glasses for eye protection" },
                new ToolRecommendation { Name = "Work Gloves", Sku = "GLOVES-WORK-L", IsAvailable = true, Price = 7.99m, Description = "Heavy-duty work gloves" },
                new ToolRecommendation { Name = "Cordless Drill", Sku = "DRILL-CORDLESS", IsAvailable = true, Price = 79.99m, Description = "18V cordless drill with battery" },
                new ToolRecommendation { Name = "Level", Sku = "LEVEL-2FT", IsAvailable = true, Price = 19.99m, Description = "2-foot aluminum level" },
                new ToolRecommendation { Name = "Tile Cutter", Sku = "TILE-CUTTER", IsAvailable = false, Price = 45.99m, Description = "Manual tile cutting tool" }
            };
            context.AddRange(tools);
        }

        // Seed Locations
        if (!context.Location.Any())
        {
            var locations = new List<StoreLocation>
            {
                new StoreLocation { Section = "Hardware Tools", Aisle = "A1", Shelf = "Middle", Description = "Hand and power tools section" },
                new StoreLocation { Section = "Paint & Supplies", Aisle = "B3", Shelf = "Top", Description = "Paint and painting supplies" },
                new StoreLocation { Section = "Garden Center", Aisle = "Outside", Shelf = "Ground Level", Description = "Outdoor garden section" },
                new StoreLocation { Section = "General Merchandise", Aisle = "C2", Shelf = "Middle", Description = "General merchandise" },
                new StoreLocation { Section = "Lumber & Building Materials", Aisle = "D1", Shelf = "Ground Level", Description = "Lumber and building materials" },
                new StoreLocation { Section = "Electrical", Aisle = "E2", Shelf = "Middle", Description = "Electrical supplies and fixtures" },
                new StoreLocation { Section = "Plumbing", Aisle = "F1", Shelf = "Bottom", Description = "Plumbing supplies and fixtures" }
            };
            context.AddRange(locations);
        }

        context.SaveChanges();
    }

    private static List<Product> GetProductsToAdd(int count, List<Product> baseProducts)
    {
        var productsToAdd = new List<Product>();
        for (int i = 1; i < count; i++)
        {
            foreach (var product in baseProducts)
            {
                var newproduct = new Product
                {
                    Name = $"{product.Name}-{i}",
                    Description = product.Description,
                    ImageUrl = product.ImageUrl,
                    Price = product.Price
                };
                productsToAdd.Add(newproduct);
            }
        }
        return productsToAdd;
    }
}
