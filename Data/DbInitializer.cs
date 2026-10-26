using Dukan.API.Models;

namespace Dukan.API.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        // Ensure database tables are created
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Staff & Demo Users if not present
        if (!context.Users.Any(u => u.Email == "admin@dukan.com"))
        {
            context.Users.Add(new User
            {
                Name = "OM General Store Admin",
                Email = "admin@dukan.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Phone = "+91 9876543210",
                Address = "Shop #4 & 5, OM General Store, Main Market, Sector 14",
                City = "Delhi",
                Pincode = "110001",
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!context.Users.Any(u => u.Email == "customer@dukan.com"))
        {
            context.Users.Add(new User
            {
                Name = "Rahul Sharma",
                Email = "customer@dukan.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                Phone = "+91 9123456780",
                Address = "Flat 402, Green Valley Apartments",
                City = "Delhi",
                Pincode = "110052",
                Role = "Customer",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 1.b Seed Order Manager account
        if (!context.Users.Any(u => u.Email == "manager@dukan.com"))
        {
            context.Users.Add(new User
            {
                Name = "Store Order Manager",
                Email = "manager@dukan.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager@123"),
                Phone = "+91 9876500002",
                Address = "Store Fulfillment Counter, Sector 14",
                City = "Delhi",
                Pincode = "110001",
                Role = "OrderManager",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 1.c Seed Delivery Partner 1 (Ramesh Kumar)
        if (!context.Users.Any(u => u.Email == "rider1@dukan.com"))
        {
            context.Users.Add(new User
            {
                Name = "Ramesh Kumar (Rider 1)",
                Email = "rider1@dukan.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Rider@123"),
                Phone = "+91 9876500001",
                Address = "Store Delivery Fleet Hub, Sector 14",
                City = "Delhi",
                Pincode = "110001",
                Role = "DeliveryBoy",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 1.d Seed Delivery Partner 2 (Suresh Sharma)
        if (!context.Users.Any(u => u.Email == "rider2@dukan.com"))
        {
            context.Users.Add(new User
            {
                Name = "Suresh Sharma (Rider 2)",
                Email = "rider2@dukan.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Rider@123"),
                Phone = "+91 9876500003",
                Address = "Store Delivery Fleet Hub, Sector 14",
                City = "Delhi",
                Pincode = "110001",
                Role = "DeliveryBoy",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 1.e Seed legacy rider@dukan.com for compatibility
        if (!context.Users.Any(u => u.Email == "rider@dukan.com"))
        {
            context.Users.Add(new User
            {
                Name = "Ramesh Kumar",
                Email = "rider@dukan.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Rider@123"),
                Phone = "+91 9876500001",
                Address = "Store Delivery Hub, Sector 14",
                City = "Delhi",
                Pincode = "110001",
                Role = "DeliveryBoy",
                CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();

        // 2. Seed Categories if none exist
        if (!context.Categories.Any())
        {
            var categories = new List<Category>
            {
                new()
                {
                    Name = "Atta, Dal & Rice",
                    Description = "Staples, whole grains, pulses, and organic flours",
                    ImageUrl = "https://images.unsplash.com/photo-1586201375761-83865001e31c?w=600&auto=format&fit=crop&q=80",
                    IsActive = true
                },
                new()
                {
                    Name = "Oil, Ghee & Masala",
                    Description = "Pure cooking oils, desi cow ghee, whole and ground spices",
                    ImageUrl = "https://images.unsplash.com/photo-1596040033229-a9821ebd058d?w=600&auto=format&fit=crop&q=80",
                    IsActive = true
                },
                new()
                {
                    Name = "Dairy, Bread & Eggs",
                    Description = "Fresh cow milk, butter, paneer, brown bread, and farm eggs",
                    ImageUrl = "https://images.unsplash.com/photo-1550583724-b2692b85b150?w=600&auto=format&fit=crop&q=80",
                    IsActive = true
                },
                new()
                {
                    Name = "Snacks & Beverages",
                    Description = "Biscuits, premium tea, filter coffee, chips, and refreshing drinks",
                    ImageUrl = "https://images.unsplash.com/photo-1559825481-12a05cc00344?w=600&auto=format&fit=crop&q=80",
                    IsActive = true
                },
                new()
                {
                    Name = "Household & Cleaning",
                    Description = "Detergent powders, floor cleaners, dishwash bars, and hygiene",
                    ImageUrl = "https://images.unsplash.com/photo-1585421514738-01798e348b17?w=600&auto=format&fit=crop&q=80",
                    IsActive = true
                }
            };

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();

            // 3. Seed Products
            var catAtta = categories[0];
            var catOil = categories[1];
            var catDairy = categories[2];
            var catSnacks = categories[3];
            var catHouse = categories[4];

            var products = new List<Product>
            {
                // Atta, Dal & Rice
                new()
                {
                    Name = "Aashirvaad Shudh Chakki Atta",
                    Description = "100% whole wheat grain atta with 0% maida for soft rotis",
                    Price = 245.00m,
                    DiscountPrice = 220.00m,
                    StockQuantity = 50,
                    Unit = "5 kg",
                    ImageUrl = "https://images.unsplash.com/photo-1509440159596-0249088772ff?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catAtta.Id,
                    IsAvailable = true
                },
                new()
                {
                    Name = "India Gate Basmati Rice (Classic)",
                    Description = "Aged extra long grain aromatic basmati rice for royal biryani & pulao",
                    Price = 520.00m,
                    DiscountPrice = 480.00m,
                    StockQuantity = 40,
                    Unit = "5 kg",
                    ImageUrl = "https://images.unsplash.com/photo-1586201375761-83865001e31c?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catAtta.Id,
                    IsAvailable = true
                },
                new()
                {
                    Name = "Tata Sampann Unpolished Toor Dal",
                    Description = "Natural protein-rich unpolished arhar dal without artificial polish",
                    Price = 175.00m,
                    DiscountPrice = 159.00m,
                    StockQuantity = 60,
                    Unit = "1 kg",
                    ImageUrl = "https://images.unsplash.com/photo-1546548970-71785318a17b?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catAtta.Id,
                    IsAvailable = true
                },
                new()
                {
                    Name = "Fortune Moong Dal Dhuli",
                    Description = "Easy to digest, high fiber and protein-rich yellow split moong dal",
                    Price = 140.00m,
                    DiscountPrice = 125.00m,
                    StockQuantity = 35,
                    Unit = "1 kg",
                    ImageUrl = "https://images.unsplash.com/photo-1585937421612-70a008356fbe?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catAtta.Id,
                    IsAvailable = true
                },

                // Oil, Ghee & Masalas
                new()
                {
                    Name = "Fortune Sunlite Refined Sunflower Oil",
                    Description = "Light and healthy cooking oil enriched with Vitamins A & D",
                    Price = 145.00m,
                    DiscountPrice = 130.00m,
                    StockQuantity = 45,
                    Unit = "1 litre",
                    ImageUrl = "https://images.unsplash.com/photo-1474979266404-7eaacbcd87c5?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catOil.Id,
                    IsAvailable = true
                },
                new()
                {
                    Name = "Amul Pure Cow Ghee",
                    Description = "Traditional aroma and rich taste made from fresh cow milk fat",
                    Price = 650.00m,
                    DiscountPrice = 610.00m,
                    StockQuantity = 25,
                    Unit = "1 litre",
                    ImageUrl = "https://images.unsplash.com/photo-1589301760014-d929f3979dbc?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catOil.Id,
                    IsAvailable = true
                },
                new()
                {
                    Name = "Catch Turmeric Powder (Haldi)",
                    Description = "Low temperature ground pure turmeric powder with high curcumin content",
                    Price = 70.00m,
                    DiscountPrice = 62.00m,
                    StockQuantity = 80,
                    Unit = "200 g",
                    ImageUrl = "https://images.unsplash.com/photo-1615485290382-441e4d049cb5?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catOil.Id,
                    IsAvailable = true
                },
                new()
                {
                    Name = "MDH Garam Masala",
                    Description = "Traditional blend of aromatic whole spices for authentic Indian curries",
                    Price = 95.00m,
                    DiscountPrice = 88.00m,
                    StockQuantity = 50,
                    Unit = "100 g",
                    ImageUrl = "https://images.unsplash.com/photo-1596040033229-a9821ebd058d?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catOil.Id,
                    IsAvailable = true
                },

                // Dairy, Bread & Eggs
                new()
                {
                    Name = "Amul Taaza Homogenised Toned Milk",
                    Description = "UHT treated fresh toned milk pack, no boiling required",
                    Price = 72.00m,
                    DiscountPrice = 70.00m,
                    StockQuantity = 30,
                    Unit = "1 litre",
                    ImageUrl = "https://images.unsplash.com/photo-1563636619-e9143da7973b?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catDairy.Id,
                    IsAvailable = true
                },
                new()
                {
                    Name = "Amul Pasteurized Butter",
                    Description = "Delicious, creamy salted table butter loved across India",
                    Price = 58.00m,
                    DiscountPrice = 56.00m,
                    StockQuantity = 50,
                    Unit = "100 g",
                    ImageUrl = "https://images.unsplash.com/photo-1589985270826-4b7bb135bc9d?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catDairy.Id,
                    IsAvailable = true
                },
                new()
                {
                    Name = "Fresh Farm White Eggs",
                    Description = "Hygienically sorted and packed fresh farm eggs rich in protein",
                    Price = 90.00m,
                    DiscountPrice = 82.00m,
                    StockQuantity = 3, // Low stock demo!
                    Unit = "Tray of 12",
                    ImageUrl = "https://images.unsplash.com/photo-1516467508483-a7212febe31a?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catDairy.Id,
                    IsAvailable = true
                },

                // Snacks & Beverages
                new()
                {
                    Name = "Tata Tea Gold",
                    Description = "Exquisite blend of Assam CTC tea with gently rolled long leaves",
                    Price = 310.00m,
                    DiscountPrice = 285.00m,
                    StockQuantity = 40,
                    Unit = "500 g",
                    ImageUrl = "https://images.unsplash.com/photo-1576092768241-dec231879fc3?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catSnacks.Id,
                    IsAvailable = true
                },
                new()
                {
                    Name = "Parle-G Gold Biscuits",
                    Description = "Classic crunchy glucose biscuits enriched with milk and wheat",
                    Price = 30.00m,
                    DiscountPrice = 28.00m,
                    StockQuantity = 100,
                    Unit = "1 kg pack",
                    ImageUrl = "https://images.unsplash.com/photo-1558961363-fa8fdf82db35?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catSnacks.Id,
                    IsAvailable = true
                },
                new()
                {
                    Name = "Haldiram's Nagpur Aloo Bhujia",
                    Description = "Crispy spicy potato sev snack prepared with traditional spices",
                    Price = 110.00m,
                    DiscountPrice = 98.00m,
                    StockQuantity = 4, // Low stock demo!
                    Unit = "400 g",
                    ImageUrl = "https://images.unsplash.com/photo-1621996346565-e3d5d6281699?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catSnacks.Id,
                    IsAvailable = true
                },

                // Household & Cleaning
                new()
                {
                    Name = "Surf Excel Quick Wash Detergent Powder",
                    Description = "X-Tra clean power dissolves easily to remove tough stains",
                    Price = 230.00m,
                    DiscountPrice = 210.00m,
                    StockQuantity = 30,
                    Unit = "1 kg",
                    ImageUrl = "https://images.unsplash.com/photo-1610557892470-55d9e80c0bce?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catHouse.Id,
                    IsAvailable = true
                },
                new()
                {
                    Name = "Vim Dishwash Gel (Lemon)",
                    Description = "Power of 100 lemons cuts through tough grease easily",
                    Price = 125.00m,
                    DiscountPrice = 115.00m,
                    StockQuantity = 45,
                    Unit = "750 ml",
                    ImageUrl = "https://images.unsplash.com/photo-1585421514738-01798e348b17?w=600&auto=format&fit=crop&q=80",
                    CategoryId = catHouse.Id,
                    IsAvailable = true
                }
            };

            context.Products.AddRange(products);
            await context.SaveChangesAsync();
        }
    }
}
