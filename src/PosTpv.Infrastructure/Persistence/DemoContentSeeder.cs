using System.Globalization;
using System.Security;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Domain.Entities;
using PosTpv.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PosTpv.Infrastructure.Persistence;

/// <summary>
/// Development-only filler that tops the database up with a large, realistic demo data set:
/// a full menu with images/allergens/extras, staff, customers, reservations, a bigger floor plan
/// with zones and decor, suppliers with documents, purchases, stock, albarán scans, three months
/// of sales history and a handful of live orders for the kitchen display.
/// Every step is additive and idempotent (matched by name or by the "H"/"L"/"D" number prefixes),
/// so it is safe to run on every start and never touches rows it didn't create beyond filling blanks.
/// </summary>
public class DemoContentSeeder
{
    private const string HistoryOrderPrefix = "O-H";
    private const string HistoryInvoicePrefix = "INV-H";
    private const string LiveOrderPrefix = "O-L";
    private const string PurchasePrefix = "ALB-D";
    private const int HistoryDays = 90;

    private readonly PosDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<DemoContentSeeder> _log;
    private readonly Random _rng = new(20260929);

    public DemoContentSeeder(PosDbContext db, IPasswordHasher hasher, ILogger<DemoContentSeeder> log)
    {
        _db = db;
        _hasher = hasher;
        _log = log;
    }

    /// <param name="webRootPath">wwwroot of the web app; supplier documents and albarán images are written under uploads/.</param>
    public async Task SeedAsync(string webRootPath, CancellationToken ct = default)
    {
        await SeedSettingsAsync(ct);
        await SeedUsersAsync(ct);
        await SeedAllergensAsync(ct);
        await SeedCatalogueAsync(ct);
        await SeedExtrasAsync(ct);
        await SeedFloorAsync(ct);
        await SeedCustomersAndReservationsAsync(ct);
        await SeedSalesHistoryAsync(ct);
        await SeedSuppliersAsync(webRootPath, ct);
        await SeedPurchasesAndStockAsync(ct);
        await SeedAlbaranScansAsync(webRootPath, ct);
        await SeedLiveOrdersAsync(ct);
    }

    // ------------------------------------------------------------------ settings & staff

    private async Task SeedSettingsAsync(CancellationToken ct)
    {
        var s = await _db.AppSettings.FirstOrDefaultAsync(ct);
        if (s is null || s.ReceiptLegalName is not null) return;
        s.ReceiptLegalName = "La Toscana Pizzeria S.L.";
        s.ReceiptTaxId = "B-00000000";
        s.ReceiptAddress = "12 Main Street, 28013 Madrid";
        s.ReceiptFooter = "Thank you for your visit! Follow us @latoscana.demo";
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        (string User, string Name, UserRole Role)[] staff =
        {
            ("maria", "María López", UserRole.Waiter),
            ("david", "David Romero", UserRole.Waiter),
            ("lucia", "Lucía Fernández", UserRole.Waiter),
            ("sergio", "Sergio Martín", UserRole.Waiter),
            ("chef", "Carlos Navarro", UserRole.Kitchen),
            ("pastry", "Elena Ruiz", UserRole.Kitchen),
            ("pablo", "Pablo Sánchez", UserRole.Cashier),
            ("manager", "Ana Torres", UserRole.Admin),
        };
        var existing = await _db.Users.Select(u => u.Username).ToListAsync(ct);
        var added = 0;
        foreach (var (user, name, role) in staff.Where(s => !existing.Contains(s.User)))
        {
            _db.Users.Add(new User { Username = user, FullName = name, Role = role, PasswordHash = _hasher.Hash("1234") });
            added++;
        }
        if (added == 0) return;
        _log.LogInformation("Demo content: adding {Count} staff accounts.", added);
        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ allergens

    private static readonly (string Code, string Name, string Emoji, string Color, string Description)[] AllergenData =
    {
        ("G", "Gluten", "🌾", "#ca8a04", "Cereals containing gluten: wheat, rye, barley, oats, spelt and their hybrids."),
        ("C", "Crustaceans", "🦐", "#ea580c", "Prawns, crab, lobster, crayfish and products made from them."),
        ("E", "Eggs", "🥚", "#eab308", "Eggs and egg-based products such as mayonnaise, custard and fresh pasta."),
        ("F", "Fish", "🐟", "#0284c7", "Fish and fish products, including anchovies, fish sauce and stocks."),
        ("P", "Peanuts", "🥜", "#a16207", "Peanuts and peanut-based products such as oils and butters."),
        ("S", "Soy", "🫘", "#65a30d", "Soybeans and soy products: tofu, soy sauce, lecithin."),
        ("M", "Milk", "🥛", "#60a5fa", "Milk and dairy products including lactose: cheese, butter, cream, yoghurt."),
        ("N", "Tree nuts", "🌰", "#92400e", "Almonds, hazelnuts, walnuts, cashews, pecans, pistachios, macadamias."),
        ("Ce", "Celery", "🥬", "#16a34a", "Celery stalks, leaves, seeds and celeriac, often found in stocks and soups."),
        ("Mu", "Mustard", "🌭", "#facc15", "Mustard seeds, powder and liquid mustard, common in dressings and sauces."),
        ("Se", "Sesame", "🫓", "#d6d3d1", "Sesame seeds and oil, tahini, hummus and some breads."),
        ("Su", "Sulphites", "🍷", "#7f1d1d", "Sulphur dioxide and sulphites above 10 mg/kg, typical of wine and dried fruit."),
        ("L", "Lupin", "🌼", "#8b5cf6", "Lupin seeds and flour, sometimes used in breads and pastries."),
        ("Mo", "Molluscs", "🦪", "#0f766e", "Mussels, clams, oysters, squid, octopus and snails."),
    };

    private async Task SeedAllergensAsync(CancellationToken ct)
    {
        var existing = await _db.Allergens.ToListAsync(ct);
        var added = 0;
        foreach (var a in AllergenData)
        {
            if (existing.Any(e => e.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase))) continue;
            _db.Allergens.Add(new Allergen { Name = a.Name, Description = a.Description, ImageUrl = BadgeImg(a.Emoji, a.Color) });
            added++;
        }
        if (added == 0) return;
        _log.LogInformation("Demo content: adding {Count} allergens.", added);
        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ catalogue

    private sealed record CatSpec(string Name, string Icon, string Color, CategoryKind Kind, CourseType Course, string[] Comments);

    private sealed record ProdSpec(string Category, string Name, decimal Price, string Emoji, string Description,
        string? Ingredients, string Allergens, int Prep, decimal Vat);

    private static readonly CatSpec[] Categories =
    {
        new("Drinks", "🥤", "#0ea5e9", CategoryKind.Drink, CourseType.Main, new[] { "No ice", "With lemon", "Very cold" }),
        new("Beers", "🍺", "#d97706", CategoryKind.Drink, CourseType.Main, new[] { "Frosted glass", "No glass" }),
        new("Wines", "🍷", "#7f1d1d", CategoryKind.Drink, CourseType.Main, new[] { "Bottle", "Decant" }),
        new("Cocktails", "🍹", "#db2777", CategoryKind.Drink, CourseType.Main, new[] { "Less sugar", "Extra ice", "No alcohol" }),
        new("Coffee & Tea", "☕", "#78350f", CategoryKind.Drink, CourseType.Dessert, new[] { "Decaf", "Very hot", "Lactose-free milk" }),
        new("Starters", "🥗", "#22c55e", CategoryKind.Food, CourseType.Starter, new[] { "To share", "No onion", "Sauce on the side" }),
        new("Salads", "🥙", "#65a30d", CategoryKind.Food, CourseType.Starter, new[] { "No dressing", "Dressing on the side", "No onion" }),
        new("Soups", "🍲", "#ea580c", CategoryKind.Food, CourseType.Starter, new[] { "Very hot", "No croutons" }),
        new("Pizzas", "🍕", "#ef4444", CategoryKind.Food, CourseType.Main, new[] { "Well done", "No cheese", "Extra sauce", "Cut in 8" }),
        new("Pasta", "🍝", "#f59e0b", CategoryKind.Food, CourseType.Main, new[] { "Spicy", "No cheese", "Al dente" }),
        new("Risotto", "🍚", "#ca8a04", CategoryKind.Food, CourseType.Main, new[] { "Extra parmesan", "No butter" }),
        new("Burgers", "🍔", "#a16207", CategoryKind.Food, CourseType.Main, new[] { "Well done", "Medium", "No pickles", "No onion" }),
        new("Meat", "🥩", "#991b1b", CategoryKind.Food, CourseType.Main, new[] { "Rare", "Medium rare", "Well done", "Sauce on the side" }),
        new("Fish & Seafood", "🐟", "#0369a1", CategoryKind.Food, CourseType.Main, new[] { "Grilled", "No garlic", "Lemon on the side" }),
        new("Kids Menu", "🧒", "#8b5cf6", CategoryKind.Food, CourseType.Main, new[] { "Small portion", "Cut into pieces" }),
        new("Desserts", "🍰", "#ec4899", CategoryKind.Food, CourseType.Dessert, new[] { "Two spoons", "Birthday candle" }),
        new("Ice Cream", "🍨", "#f472b6", CategoryKind.Food, CourseType.Dessert, new[] { "In a cone", "In a cup" }),
    };

    private static readonly ProdSpec[] Products =
    {
        // Drinks
        new("Drinks", "Water", 1.50m, "💧", "Still mineral water, 50 cl.", null, "", 0, 10),
        new("Drinks", "Sparkling Water", 1.80m, "🫧", "Naturally sparkling mineral water, 50 cl.", null, "", 0, 10),
        new("Drinks", "Large Mineral Water", 2.90m, "💧", "Still mineral water, 1 l bottle to share.", null, "", 0, 10),
        new("Drinks", "Coca-Cola", 2.20m, "🥤", "Classic cola, 33 cl can served with ice and lemon.", null, "", 0, 10),
        new("Drinks", "Coca-Cola Zero", 2.20m, "🥤", "Sugar-free cola, 33 cl.", null, "", 0, 10),
        new("Drinks", "Orange Soda", 2.20m, "🍊", "Fizzy orange soft drink, 33 cl.", null, "", 0, 10),
        new("Drinks", "Lemon Soda", 2.20m, "🍋", "Fizzy lemon soft drink, 33 cl.", null, "", 0, 10),
        new("Drinks", "Iced Tea", 2.30m, "🧊", "Lemon iced tea, 33 cl.", null, "", 0, 10),
        new("Drinks", "Tonic Water", 2.40m, "🫧", "Premium tonic water, 20 cl.", null, "", 0, 10),
        new("Drinks", "Lemonade", 2.60m, "🍋", "Homemade lemonade with fresh mint.", "Lemon, sugar, mint, water", "", 0, 10),
        new("Drinks", "Orange Juice", 3.20m, "🍊", "Freshly squeezed orange juice.", "Oranges", "", 2, 10),
        new("Drinks", "Apple Juice", 2.60m, "🍏", "Cloudy pressed apple juice.", "Apples", "", 0, 10),
        new("Drinks", "Pineapple Juice", 2.60m, "🍍", "Tropical pineapple juice.", "Pineapple", "", 0, 10),
        new("Drinks", "Strawberry Smoothie", 4.20m, "🍓", "Strawberry, banana and yoghurt smoothie.", "Strawberries, banana, yoghurt, honey", "M", 3, 10),
        new("Drinks", "Hot Chocolate", 2.70m, "🍫", "Thick Spanish-style hot chocolate.", "Milk, cocoa, sugar", "M", 2, 10),
        // Beers
        new("Beers", "Draft Beer", 2.50m, "🍺", "Local lager on tap, 33 cl.", "Water, barley malt, hops", "G", 0, 21),
        new("Beers", "Pint of Beer", 4.20m, "🍺", "Local lager on tap, 50 cl.", "Water, barley malt, hops", "G", 0, 21),
        new("Beers", "Italian Lager", 3.50m, "🍻", "Crisp Italian lager, 33 cl bottle.", "Water, barley malt, maize, hops", "G", 0, 21),
        new("Beers", "Craft IPA", 4.80m, "🍺", "Hoppy India Pale Ale with citrus notes, 33 cl.", "Water, barley malt, hops, yeast", "G", 0, 21),
        new("Beers", "Wheat Beer", 4.50m, "🍺", "Unfiltered wheat beer with banana and clove aroma, 50 cl.", "Water, wheat malt, barley malt, hops", "G", 0, 21),
        new("Beers", "Stout", 4.90m, "🍺", "Dark roasted stout with coffee notes, 33 cl.", "Water, roasted barley, hops", "G", 0, 21),
        new("Beers", "Gluten-Free Beer", 3.80m, "🍺", "Gluten-free lager, 33 cl.", "Water, deglutenised barley malt, hops", "", 0, 21),
        new("Beers", "Alcohol-Free Beer", 2.80m, "🍺", "0.0 lager, 33 cl.", "Water, barley malt, hops", "G", 0, 10),
        new("Beers", "Shandy", 2.60m, "🍋", "Lager with lemon soda.", "Beer, lemon soda", "G", 0, 21),
        new("Beers", "Cider", 4.20m, "🍏", "Dry apple cider, 33 cl.", "Apples", "Su", 0, 21),
        // Wines
        new("Wines", "House Wine", 3.50m, "🍷", "Glass of our house red or white.", null, "Su", 0, 21),
        new("Wines", "Red Wine", 3.80m, "🍷", "Glass of Rioja Crianza.", null, "Su", 0, 21),
        new("Wines", "White Wine", 3.80m, "🥂", "Glass of Rueda Verdejo.", null, "Su", 0, 21),
        new("Wines", "Rosé Wine", 3.60m, "🌸", "Glass of Navarra rosé.", null, "Su", 0, 21),
        new("Wines", "Chianti Bottle", 22.00m, "🍷", "Chianti Classico DOCG, 75 cl.", null, "Su", 0, 21),
        new("Wines", "Rioja Reserva Bottle", 26.00m, "🍷", "Rioja Reserva, 75 cl.", null, "Su", 0, 21),
        new("Wines", "Albariño Bottle", 21.00m, "🥂", "Rías Baixas Albariño, 75 cl.", null, "Su", 0, 21),
        new("Wines", "Prosecco", 4.50m, "🥂", "Glass of Prosecco DOC.", null, "Su", 0, 21),
        new("Wines", "Prosecco Bottle", 24.00m, "🍾", "Prosecco Extra Dry, 75 cl.", null, "Su", 0, 21),
        new("Wines", "Lambrusco", 3.90m, "🍷", "Glass of sparkling red Lambrusco.", null, "Su", 0, 21),
        new("Wines", "Sangria", 4.20m, "🍹", "Red wine sangria with fresh fruit.", "Red wine, orange, lemon, apple, cinnamon", "Su", 2, 21),
        new("Wines", "Sangria Jug", 15.00m, "🍹", "1 l jug of sangria to share.", "Red wine, orange, lemon, apple, cinnamon", "Su", 3, 21),
        // Cocktails
        new("Cocktails", "Aperol Spritz", 7.50m, "🍹", "Aperol, prosecco and a splash of soda.", "Aperol, prosecco, soda, orange", "Su", 3, 21),
        new("Cocktails", "Negroni", 8.50m, "🥃", "Gin, Campari and red vermouth.", "Gin, Campari, vermouth, orange", "Su", 3, 21),
        new("Cocktails", "Mojito", 8.00m, "🍸", "Rum, lime, mint and sugar cane.", "Rum, lime, mint, sugar, soda", "", 4, 21),
        new("Cocktails", "Margarita", 8.00m, "🍸", "Tequila, triple sec and lime with a salted rim.", "Tequila, triple sec, lime, salt", "", 3, 21),
        new("Cocktails", "Gin & Tonic", 9.00m, "🍸", "Premium gin with tonic, juniper and citrus peel.", "Gin, tonic, juniper, lemon", "", 3, 21),
        new("Cocktails", "Espresso Martini", 9.00m, "🍸", "Vodka, coffee liqueur and fresh espresso.", "Vodka, coffee liqueur, espresso", "", 4, 21),
        new("Cocktails", "Limoncello Spritz", 7.50m, "🍋", "Limoncello, prosecco and soda.", "Limoncello, prosecco, soda, mint", "Su", 3, 21),
        new("Cocktails", "Piña Colada", 8.50m, "🍍", "Rum, coconut cream and pineapple.", "Rum, coconut cream, pineapple", "", 4, 21),
        new("Cocktails", "Virgin Mojito", 5.50m, "🌿", "Alcohol-free mojito with lime and mint.", "Lime, mint, sugar, soda", "", 3, 10),
        new("Cocktails", "Limoncello Shot", 3.00m, "🍋", "Chilled limoncello from Amalfi.", null, "", 0, 21),
        // Coffee & tea
        new("Coffee & Tea", "Espresso", 1.60m, "☕", "Single shot of Italian espresso.", "Arabica coffee", "", 1, 10),
        new("Coffee & Tea", "Double Espresso", 2.20m, "☕", "Double shot of espresso.", "Arabica coffee", "", 1, 10),
        new("Coffee & Tea", "Macchiato", 1.80m, "☕", "Espresso with a dash of foamed milk.", "Coffee, milk", "M", 1, 10),
        new("Coffee & Tea", "Cappuccino", 2.40m, "☕", "Espresso with steamed milk and foam.", "Coffee, milk", "M", 2, 10),
        new("Coffee & Tea", "Latte", 2.60m, "🥛", "Espresso with plenty of steamed milk.", "Coffee, milk", "M", 2, 10),
        new("Coffee & Tea", "Americano", 1.90m, "☕", "Espresso lengthened with hot water.", "Arabica coffee", "", 1, 10),
        new("Coffee & Tea", "Affogato", 4.50m, "🍨", "Vanilla ice cream drowned in hot espresso.", "Coffee, vanilla ice cream", "M,E", 2, 10),
        new("Coffee & Tea", "Irish Coffee", 6.00m, "🥃", "Coffee, Irish whiskey and whipped cream.", "Coffee, whiskey, cream, sugar", "M", 3, 21),
        new("Coffee & Tea", "Green Tea", 2.00m, "🍵", "Japanese sencha green tea.", null, "", 1, 10),
        new("Coffee & Tea", "Chamomile", 2.00m, "🌼", "Chamomile herbal infusion.", null, "", 1, 10),
        new("Coffee & Tea", "Earl Grey", 2.00m, "🫖", "Black tea with bergamot.", null, "", 1, 10),
        new("Coffee & Tea", "Mint Tea", 2.00m, "🌿", "Fresh mint infusion.", null, "", 1, 10),
        // Starters
        new("Starters", "Bruschetta", 5.90m, "🍅", "Toasted bread with tomato, garlic, basil and olive oil.", "Bread, tomato, garlic, basil, olive oil", "G", 6, 10),
        new("Starters", "Garlic Bread", 4.50m, "🥖", "Baked garlic and parsley butter bread.", "Bread, butter, garlic, parsley", "G,M", 5, 10),
        new("Starters", "Garlic Bread with Cheese", 5.50m, "🧀", "Garlic bread topped with melted mozzarella.", "Bread, butter, garlic, mozzarella", "G,M", 6, 10),
        new("Starters", "Burrata", 11.50m, "🧀", "Creamy burrata with cherry tomatoes, pesto and rocket.", "Burrata, cherry tomatoes, pesto, rocket", "M,N", 5, 10),
        new("Starters", "Beef Carpaccio", 12.50m, "🥩", "Thin slices of beef with parmesan, rocket and lemon.", "Beef, parmesan, rocket, lemon, olive oil", "M", 7, 10),
        new("Starters", "Antipasto Platter", 15.90m, "🍖", "Cured meats, cheeses, olives and grissini to share.", "Prosciutto, salami, mortadella, pecorino, olives", "G,M,Su", 8, 10),
        new("Starters", "Arancini", 7.50m, "🍙", "Crispy risotto balls filled with ragù and mozzarella.", "Rice, beef ragù, mozzarella, breadcrumbs", "G,M,E,Ce", 9, 10),
        new("Starters", "Fried Calamari", 9.50m, "🦑", "Crispy squid rings with lemon aioli.", "Squid, flour, aioli, lemon", "G,E,Mo", 9, 10),
        new("Starters", "Mozzarella Sticks", 6.50m, "🧀", "Breaded mozzarella with tomato dip.", "Mozzarella, breadcrumbs, egg, tomato", "G,M,E", 7, 10),
        new("Starters", "Chicken Wings", 7.90m, "🍗", "Crispy wings with BBQ or buffalo sauce.", "Chicken, BBQ sauce", "Mu,Ce", 12, 10),
        new("Starters", "Nachos", 8.50m, "🌮", "Tortilla chips with cheese, guacamole, sour cream and jalapeños.", "Corn chips, cheddar, avocado, sour cream, jalapeños", "M", 7, 10),
        new("Starters", "Patatas Bravas", 5.90m, "🥔", "Fried potatoes with spicy brava sauce and aioli.", "Potatoes, brava sauce, aioli", "E", 8, 10),
        new("Starters", "Hummus & Pita", 6.00m, "🫓", "Chickpea hummus with warm pita bread.", "Chickpeas, tahini, lemon, pita", "G,Se", 5, 10),
        new("Starters", "Spring Rolls", 5.80m, "🥟", "Crispy vegetable spring rolls with sweet chilli sauce.", "Cabbage, carrot, rice paper, chilli sauce", "G,S", 7, 10),
        new("Starters", "Stuffed Mushrooms", 6.20m, "🍄", "Baked mushrooms stuffed with cheese and herbs.", "Mushrooms, ricotta, parmesan, herbs", "M", 9, 10),
        new("Starters", "Onion Rings", 5.20m, "🧅", "Beer-battered onion rings.", "Onion, flour, beer", "G", 6, 10),
        new("Starters", "Prawn Tempura", 10.50m, "🍤", "Tempura king prawns with soy dipping sauce.", "Prawns, flour, soy sauce", "G,C,S", 8, 10),
        new("Starters", "Croquettes", 7.20m, "🥟", "Homemade Iberian ham croquettes (6 units).", "Ham, milk, flour, breadcrumbs, egg", "G,M,E", 7, 10),
        // Salads
        new("Salads", "Caesar Salad", 9.50m, "🥗", "Romaine, grilled chicken, parmesan, croutons and Caesar dressing.", "Romaine, chicken, parmesan, croutons, anchovy dressing", "G,M,E,F", 8, 10),
        new("Salads", "Caprese Salad", 8.90m, "🍅", "Tomato, fresh mozzarella and basil.", "Tomato, mozzarella, basil, olive oil", "M", 5, 10),
        new("Salads", "Greek Salad", 8.50m, "🫒", "Tomato, cucumber, feta, olives and red onion.", "Tomato, cucumber, feta, olives, onion", "M", 6, 10),
        new("Salads", "Goat Cheese Salad", 10.50m, "🥗", "Warm goat cheese, walnuts, honey and mixed leaves.", "Goat cheese, walnuts, honey, leaves", "M,N", 7, 10),
        new("Salads", "Quinoa Salad", 9.20m, "🥗", "Quinoa, avocado, roasted vegetables and lemon dressing.", "Quinoa, avocado, peppers, courgette", "", 6, 10),
        new("Salads", "Tuna Salad", 9.60m, "🐟", "Tuna, egg, potato, green beans and olives.", "Tuna, egg, potato, green beans, olives", "F,E", 6, 10),
        new("Salads", "Burrata & Peach Salad", 11.90m, "🍑", "Burrata, grilled peach, prosciutto and basil.", "Burrata, peach, prosciutto, basil", "M", 6, 10),
        new("Salads", "Mixed Green Salad", 6.90m, "🥬", "Seasonal leaves, tomato, carrot and sweetcorn.", "Lettuce, tomato, carrot, corn", "", 5, 10),
        new("Salads", "Panzanella", 8.20m, "🥖", "Tuscan bread salad with tomato and cucumber.", "Bread, tomato, cucumber, onion, basil", "G", 6, 10),
        // Soups
        new("Soups", "Minestrone", 7.50m, "🍲", "Hearty Italian vegetable soup with pasta.", "Beans, carrot, celery, pasta, tomato", "G,Ce", 6, 10),
        new("Soups", "Tomato Soup", 6.90m, "🍅", "Roasted tomato and basil soup with croutons.", "Tomato, basil, cream, croutons", "G,M", 5, 10),
        new("Soups", "Gazpacho", 6.50m, "🥒", "Chilled Andalusian tomato soup.", "Tomato, cucumber, pepper, garlic, olive oil", "", 3, 10),
        new("Soups", "Mushroom Cream Soup", 7.20m, "🍄", "Creamy wild mushroom soup with truffle oil.", "Mushrooms, cream, onion, truffle oil", "M,Ce", 6, 10),
        new("Soups", "Seafood Soup", 10.90m, "🦐", "Rich soup with prawns, mussels and fish.", "Prawns, mussels, hake, tomato", "C,F,Mo,Ce", 10, 10),
        new("Soups", "Onion Soup", 7.50m, "🧅", "French onion soup gratinated with cheese.", "Onion, beef stock, bread, gruyère", "G,M,Ce", 8, 10),
        // Pizzas
        new("Pizzas", "Margherita", 8.50m, "🍕", "Tomato, fior di latte mozzarella and fresh basil.", "Tomato, mozzarella, basil, olive oil", "G,M", 12, 10),
        new("Pizzas", "Marinara", 7.50m, "🍕", "Tomato, garlic, oregano and olive oil. Vegan.", "Tomato, garlic, oregano, olive oil", "G", 11, 10),
        new("Pizzas", "Pepperoni", 10.90m, "🍕", "Tomato, mozzarella and spicy pepperoni.", "Tomato, mozzarella, pepperoni", "G,M", 12, 10),
        new("Pizzas", "Four Cheese", 11.50m, "🧀", "Mozzarella, gorgonzola, parmesan and goat cheese.", "Mozzarella, gorgonzola, parmesan, goat cheese", "G,M", 13, 10),
        new("Pizzas", "Diavola", 11.90m, "🌶️", "Tomato, mozzarella, spicy salami and chilli.", "Tomato, mozzarella, spicy salami, chilli", "G,M", 13, 10),
        new("Pizzas", "Vegetariana", 10.50m, "🫑", "Tomato, mozzarella and grilled seasonal vegetables.", "Tomato, mozzarella, peppers, aubergine, courgette, onion", "G,M", 12, 10),
        new("Pizzas", "Quattro Stagioni", 12.20m, "🍕", "Ham, mushrooms, artichokes and olives.", "Tomato, mozzarella, ham, mushrooms, artichokes, olives", "G,M", 14, 10),
        new("Pizzas", "Hawaiana", 10.90m, "🍍", "Tomato, mozzarella, ham and pineapple.", "Tomato, mozzarella, ham, pineapple", "G,M", 12, 10),
        new("Pizzas", "Prosciutto e Rucola", 12.50m, "🍕", "Mozzarella, prosciutto di Parma, rocket and parmesan.", "Tomato, mozzarella, prosciutto, rocket, parmesan", "G,M", 13, 10),
        new("Pizzas", "Funghi", 10.20m, "🍄", "Tomato, mozzarella and sautéed mushrooms.", "Tomato, mozzarella, mushrooms", "G,M", 12, 10),
        new("Pizzas", "Capricciosa", 11.90m, "🍕", "Ham, mushrooms, artichokes, olives and egg.", "Tomato, mozzarella, ham, mushrooms, artichokes, egg", "G,M,E", 13, 10),
        new("Pizzas", "Tonno e Cipolla", 11.20m, "🐟", "Tomato, mozzarella, tuna and red onion.", "Tomato, mozzarella, tuna, onion", "G,M,F", 12, 10),
        new("Pizzas", "BBQ Chicken", 12.50m, "🍗", "BBQ sauce, chicken, red onion and smoked cheese.", "BBQ sauce, chicken, onion, smoked provola", "G,M,Mu", 13, 10),
        new("Pizzas", "Carbonara Pizza", 12.20m, "🥓", "Cream, guanciale, egg yolk and pecorino.", "Cream, guanciale, egg, pecorino", "G,M,E", 13, 10),
        new("Pizzas", "Truffle", 14.90m, "🍄", "Truffle cream, mozzarella, mushrooms and truffle shavings.", "Truffle cream, mozzarella, mushrooms, black truffle", "G,M", 14, 10),
        new("Pizzas", "Calzone", 11.50m, "🥟", "Folded pizza with ham, ricotta, mozzarella and tomato.", "Ham, ricotta, mozzarella, tomato", "G,M", 15, 10),
        new("Pizzas", "Seafood Pizza", 13.90m, "🦐", "Tomato, prawns, mussels, squid and garlic.", "Tomato, prawns, mussels, squid, garlic", "G,C,Mo", 14, 10),
        new("Pizzas", "Nduja", 12.90m, "🌶️", "Spicy Calabrian nduja, burrata and honey.", "Tomato, nduja, burrata, honey", "G,M", 13, 10),
        new("Pizzas", "Pistacchio e Mortadella", 13.50m, "🌰", "Pistachio cream, mortadella and stracciatella.", "Pistachio cream, mortadella, stracciatella", "G,M,N", 13, 10),
        new("Pizzas", "Napoletana", 9.20m, "🍕", "Tomato, mozzarella, anchovies, capers and oregano.", "Tomato, mozzarella, anchovies, capers", "G,M,F", 12, 10),
        // Pasta
        new("Pasta", "Carbonara", 10.90m, "🍝", "Spaghetti with guanciale, egg yolk, pecorino and black pepper.", "Spaghetti, guanciale, egg, pecorino", "G,E,M", 11, 10),
        new("Pasta", "Bolognese", 10.50m, "🍝", "Tagliatelle with slow-cooked beef ragù.", "Tagliatelle, beef, tomato, carrot, celery", "G,E,Ce", 11, 10),
        new("Pasta", "Lasagna", 11.90m, "🍝", "Baked layers of pasta, ragù, béchamel and parmesan.", "Pasta, beef ragù, béchamel, parmesan", "G,E,M,Ce", 15, 10),
        new("Pasta", "Pesto Genovese", 10.20m, "🌿", "Trofie with basil pesto, potatoes and green beans.", "Trofie, basil, pine nuts, parmesan, potato", "G,M,N", 10, 10),
        new("Pasta", "Arrabbiata", 9.50m, "🌶️", "Penne with spicy tomato and garlic sauce.", "Penne, tomato, garlic, chilli", "G", 10, 10),
        new("Pasta", "Amatriciana", 10.50m, "🍝", "Bucatini with guanciale, tomato and pecorino.", "Bucatini, guanciale, tomato, pecorino", "G,M", 11, 10),
        new("Pasta", "Cacio e Pepe", 10.20m, "🧀", "Tonnarelli with pecorino and black pepper.", "Tonnarelli, pecorino, black pepper", "G,M", 10, 10),
        new("Pasta", "Seafood Linguine", 14.50m, "🦐", "Linguine with prawns, clams and mussels.", "Linguine, prawns, clams, mussels, garlic", "G,C,Mo", 14, 10),
        new("Pasta", "Spaghetti alle Vongole", 13.50m, "🐚", "Spaghetti with clams, white wine and parsley.", "Spaghetti, clams, white wine, garlic", "G,Mo,Su", 12, 10),
        new("Pasta", "Ravioli Ricotta e Spinaci", 11.50m, "🥟", "Ricotta and spinach ravioli with butter and sage.", "Pasta, ricotta, spinach, butter, sage", "G,E,M", 11, 10),
        new("Pasta", "Gnocchi Sorrentina", 10.90m, "🥔", "Potato gnocchi baked with tomato and mozzarella.", "Gnocchi, tomato, mozzarella, basil", "G,M,E", 12, 10),
        new("Pasta", "Fettuccine Alfredo", 10.50m, "🍝", "Fettuccine in a creamy parmesan sauce.", "Fettuccine, butter, cream, parmesan", "G,M,E", 11, 10),
        new("Pasta", "Tortellini in Brodo", 10.90m, "🍜", "Meat tortellini in a rich capon broth.", "Tortellini, pork, prosciutto, broth", "G,E,M,Ce", 10, 10),
        new("Pasta", "Penne al Salmone", 11.90m, "🐟", "Penne with smoked salmon, cream and dill.", "Penne, smoked salmon, cream, dill", "G,M,F", 11, 10),
        // Risotto
        new("Risotto", "Mushroom Risotto", 12.50m, "🍄", "Carnaroli rice with porcini mushrooms and parmesan.", "Rice, porcini, parmesan, butter, white wine", "M,Su,Ce", 18, 10),
        new("Risotto", "Seafood Risotto", 15.50m, "🦐", "Creamy risotto with prawns, mussels and squid.", "Rice, prawns, mussels, squid, fish stock", "C,Mo,F,Ce", 20, 10),
        new("Risotto", "Risotto alla Milanese", 12.90m, "🍚", "Saffron risotto with bone marrow and parmesan.", "Rice, saffron, bone marrow, parmesan", "M,Ce", 18, 10),
        new("Risotto", "Asparagus Risotto", 12.20m, "🌱", "Green asparagus and lemon zest risotto.", "Rice, asparagus, lemon, parmesan", "M,Ce", 18, 10),
        new("Risotto", "Truffle Risotto", 16.90m, "🍄", "Risotto with black truffle and aged parmesan.", "Rice, black truffle, parmesan, butter", "M,Ce", 18, 10),
        new("Risotto", "Pumpkin Risotto", 11.90m, "🎃", "Roasted pumpkin risotto with amaretti crumble.", "Rice, pumpkin, amaretti, parmesan", "M,N,G,Ce", 18, 10),
        // Burgers
        new("Burgers", "Classic Burger", 11.50m, "🍔", "Beef patty, lettuce, tomato, onion and house sauce.", "Beef, brioche bun, lettuce, tomato, onion", "G,E,Se,Mu", 12, 10),
        new("Burgers", "Cheeseburger", 12.00m, "🍔", "Beef patty with double cheddar and pickles.", "Beef, brioche bun, cheddar, pickles", "G,M,E,Se,Mu", 12, 10),
        new("Burgers", "Bacon Burger", 12.90m, "🥓", "Beef patty, crispy bacon, cheddar and BBQ sauce.", "Beef, bacon, cheddar, BBQ sauce", "G,M,E,Se,Mu", 13, 10),
        new("Burgers", "Double Smash Burger", 14.50m, "🍔", "Two smashed patties, American cheese and onions.", "Beef, American cheese, onion, pickles", "G,M,E,Se,Mu", 13, 10),
        new("Burgers", "Truffle Burger", 15.50m, "🍄", "Beef, truffle mayo, mushrooms and provolone.", "Beef, truffle mayo, mushrooms, provolone", "G,M,E,Se", 14, 10),
        new("Burgers", "Italian Burger", 14.20m, "🍅", "Beef, mozzarella, pesto, sun-dried tomato and rocket.", "Beef, mozzarella, pesto, tomato, rocket", "G,M,N,Se", 13, 10),
        new("Burgers", "Crispy Chicken Burger", 11.90m, "🍗", "Fried chicken thigh, slaw and spicy mayo.", "Chicken, slaw, spicy mayo, bun", "G,E,Se,Mu", 12, 10),
        new("Burgers", "Veggie Burger", 11.50m, "🌱", "Plant-based patty, avocado, tomato and vegan mayo.", "Plant-based patty, avocado, tomato", "G,S,Se", 12, 10),
        new("Burgers", "Pulled Pork Burger", 13.50m, "🐖", "Slow-cooked pulled pork, BBQ sauce and coleslaw.", "Pork, BBQ sauce, coleslaw, bun", "G,E,Se,Mu", 13, 10),
        // Meat
        new("Meat", "Ribeye Steak", 24.90m, "🥩", "300 g aged ribeye with roasted potatoes.", "Beef ribeye, potatoes, rosemary", "", 18, 10),
        new("Meat", "Beef Tenderloin", 26.50m, "🥩", "200 g tenderloin with pepper sauce.", "Beef tenderloin, pepper sauce", "M", 18, 10),
        new("Meat", "Tagliata", 21.90m, "🥩", "Sliced sirloin with rocket, cherry tomatoes and parmesan.", "Sirloin, rocket, tomatoes, parmesan", "M", 16, 10),
        new("Meat", "Chicken Milanese", 15.50m, "🍗", "Breaded chicken breast with salad and lemon.", "Chicken, breadcrumbs, egg, salad", "G,E", 14, 10),
        new("Meat", "Saltimbocca", 17.90m, "🍖", "Veal escalopes with prosciutto and sage.", "Veal, prosciutto, sage, butter, white wine", "M,Su", 15, 10),
        new("Meat", "Osso Buco", 19.90m, "🍖", "Braised veal shank with gremolata and saffron rice.", "Veal shank, vegetables, rice, saffron", "Ce,Su", 20, 10),
        new("Meat", "Lamb Chops", 22.50m, "🍖", "Grilled lamb chops with rosemary potatoes.", "Lamb, potatoes, rosemary, garlic", "", 18, 10),
        new("Meat", "BBQ Ribs", 18.90m, "🍖", "Slow-cooked pork ribs with BBQ glaze and fries.", "Pork ribs, BBQ sauce, fries", "Mu,Ce", 16, 10),
        new("Meat", "Chicken Parmigiana", 16.50m, "🍗", "Breaded chicken with tomato, mozzarella and parmesan.", "Chicken, tomato, mozzarella, parmesan, breadcrumbs", "G,M,E", 16, 10),
        // Fish & seafood
        new("Fish & Seafood", "Grilled Salmon", 18.90m, "🐟", "Salmon fillet with grilled vegetables and lemon butter.", "Salmon, vegetables, butter, lemon", "F,M", 15, 10),
        new("Fish & Seafood", "Sea Bass", 19.50m, "🐟", "Oven-baked sea bass with potatoes and cherry tomatoes.", "Sea bass, potatoes, tomatoes, olives", "F", 18, 10),
        new("Fish & Seafood", "Grilled Octopus", 21.50m, "🐙", "Octopus with paprika potato purée.", "Octopus, potatoes, paprika, olive oil", "Mo,M", 16, 10),
        new("Fish & Seafood", "Mussels Marinara", 14.50m, "🦪", "Mussels in white wine, tomato and garlic.", "Mussels, tomato, garlic, white wine", "Mo,Su", 12, 10),
        new("Fish & Seafood", "Garlic Prawns", 15.90m, "🍤", "Sizzling prawns in garlic and chilli oil.", "Prawns, garlic, chilli, olive oil", "C", 10, 10),
        new("Fish & Seafood", "Tuna Tataki", 19.90m, "🍣", "Seared tuna with sesame, soy and wasabi mayo.", "Tuna, sesame, soy sauce, wasabi mayo", "F,S,Se,E", 12, 10),
        new("Fish & Seafood", "Fish & Chips", 15.50m, "🐟", "Beer-battered cod with chips and tartar sauce.", "Cod, flour, beer, potatoes, tartar sauce", "F,G,E", 14, 10),
        new("Fish & Seafood", "Seafood Paella", 19.50m, "🥘", "Saffron rice with prawns, mussels, clams and squid (per person).", "Rice, prawns, mussels, clams, squid, saffron", "C,Mo,F,Ce", 25, 10),
        // Kids
        new("Kids Menu", "Kids Pizza", 7.50m, "🍕", "Small margherita or ham pizza.", "Tomato, mozzarella, ham", "G,M", 10, 10),
        new("Kids Menu", "Kids Pasta", 7.00m, "🍝", "Penne with tomato sauce or butter.", "Penne, tomato", "G", 8, 10),
        new("Kids Menu", "Chicken Nuggets", 7.50m, "🍗", "Crispy nuggets with fries.", "Chicken, breadcrumbs, potatoes", "G,E", 9, 10),
        new("Kids Menu", "Mini Burger", 7.90m, "🍔", "Small beef burger with cheese and fries.", "Beef, cheese, bun, potatoes", "G,M,Se", 10, 10),
        new("Kids Menu", "Fish Fingers", 7.50m, "🐟", "Breaded fish fingers with mashed potato.", "Hake, breadcrumbs, potatoes, milk", "F,G,M", 9, 10),
        // Desserts
        new("Desserts", "Tiramisu", 6.50m, "🍰", "Classic tiramisu with mascarpone and espresso.", "Mascarpone, ladyfingers, coffee, cocoa, egg", "G,E,M", 3, 10),
        new("Desserts", "Panna Cotta", 5.50m, "🍮", "Vanilla panna cotta with berry coulis.", "Cream, vanilla, gelatine, berries", "M", 3, 10),
        new("Desserts", "Cheesecake", 6.20m, "🍰", "Baked New York cheesecake with red fruit.", "Cream cheese, biscuit, butter, egg", "G,E,M", 3, 10),
        new("Desserts", "Chocolate Lava Cake", 6.90m, "🍫", "Warm chocolate fondant with vanilla ice cream.", "Chocolate, butter, egg, flour", "G,E,M", 8, 10),
        new("Desserts", "Cannoli", 5.90m, "🥐", "Sicilian cannoli with ricotta and pistachio.", "Pastry, ricotta, pistachio, chocolate", "G,M,N", 3, 10),
        new("Desserts", "Crème Brûlée", 5.90m, "🍮", "Vanilla custard with caramelised sugar.", "Cream, egg yolk, vanilla, sugar", "E,M", 4, 10),
        new("Desserts", "Apple Pie", 5.40m, "🥧", "Warm apple pie with cinnamon.", "Apple, pastry, cinnamon, butter", "G,M,E", 4, 10),
        new("Desserts", "Lemon Tart", 5.30m, "🍋", "Tangy lemon curd tart.", "Lemon, pastry, egg, butter", "G,E,M", 3, 10),
        new("Desserts", "Chocolate Brownie", 5.50m, "🍫", "Walnut brownie with chocolate sauce.", "Chocolate, walnuts, butter, egg", "G,E,M,N", 4, 10),
        new("Desserts", "Pizza Nutella", 8.90m, "🍕", "Dessert pizza with Nutella and hazelnuts, to share.", "Pizza dough, Nutella, hazelnuts", "G,M,N,S", 8, 10),
        new("Desserts", "Fruit Salad", 4.90m, "🍓", "Fresh seasonal fruit.", "Seasonal fruit", "", 3, 10),
        new("Desserts", "Profiteroles", 6.20m, "🍩", "Choux buns with cream and hot chocolate.", "Choux pastry, cream, chocolate", "G,E,M", 4, 10),
        // Ice cream
        new("Ice Cream", "Gelato Vanilla", 4.50m, "🍨", "Two scoops of Madagascar vanilla gelato.", "Milk, cream, vanilla", "M,E", 2, 10),
        new("Ice Cream", "Gelato Chocolate", 4.50m, "🍫", "Two scoops of dark chocolate gelato.", "Milk, cocoa, sugar", "M", 2, 10),
        new("Ice Cream", "Gelato Pistachio", 4.90m, "🌰", "Two scoops of Bronte pistachio gelato.", "Milk, pistachio, sugar", "M,N", 2, 10),
        new("Ice Cream", "Gelato Stracciatella", 4.50m, "🍦", "Fior di latte with chocolate chips.", "Milk, cream, chocolate", "M", 2, 10),
        new("Ice Cream", "Lemon Sorbet", 4.20m, "🍋", "Refreshing Sicilian lemon sorbet.", "Lemon, sugar, water", "", 2, 10),
        new("Ice Cream", "Mango Sorbet", 4.20m, "🥭", "Vegan mango sorbet.", "Mango, sugar, water", "", 2, 10),
        new("Ice Cream", "Sgroppino", 5.90m, "🍋", "Lemon sorbet whisked with prosecco and vodka.", "Lemon sorbet, prosecco, vodka", "Su", 3, 21),
        new("Ice Cream", "Banana Split", 6.90m, "🍌", "Banana, three scoops, chocolate sauce and cream.", "Banana, gelato, chocolate, cream, almonds", "M,N", 4, 10),
        new("Ice Cream", "Ice Cream Sundae", 6.50m, "🍨", "Vanilla and strawberry gelato with toppings.", "Gelato, strawberry sauce, cream, wafer", "M,G,E", 4, 10),
    };

    private async Task SeedCatalogueAsync(CancellationToken ct)
    {
        var cats = await _db.Categories.Include(c => c.Comments).ToListAsync(ct);
        var nextCatOrder = cats.Count == 0 ? 1 : cats.Max(c => c.DisplayOrder) + 1;
        var catsAdded = 0;
        foreach (var spec in Categories)
        {
            var cat = cats.FirstOrDefault(c => c.Name.Equals(spec.Name, StringComparison.OrdinalIgnoreCase));
            if (cat is null)
            {
                cat = new Category
                {
                    Name = spec.Name, Icon = spec.Icon, Color = spec.Color, Kind = spec.Kind, Course = spec.Course,
                    DisplayOrder = nextCatOrder++, ImageUrl = FoodImg(spec.Icon, spec.Color),
                };
                _db.Categories.Add(cat);
                cats.Add(cat);
                catsAdded++;
            }
            cat.ImageUrl ??= FoodImg(cat.Icon ?? spec.Icon, cat.Color);
            var order = cat.Comments.Count == 0 ? 0 : cat.Comments.Max(c => c.DisplayOrder) + 1;
            foreach (var text in spec.Comments.Where(t => !cat.Comments.Any(c => c.Text.Equals(t, StringComparison.OrdinalIgnoreCase))))
                cat.Comments.Add(new CategoryComment { Text = text, DisplayOrder = order++ });
        }
        await _db.SaveChangesAsync(ct);

        var allergens = await _db.Allergens.ToListAsync(ct);
        var allergenByCode = AllergenData.ToDictionary(a => a.Code,
            a => allergens.First(x => x.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase)));

        var products = await _db.Products.Include(p => p.Allergens).ToListAsync(ct);
        var prodAdded = 0;
        foreach (var group in Products.GroupBy(p => p.Category))
        {
            var cat = cats.First(c => c.Name.Equals(group.Key, StringComparison.OrdinalIgnoreCase));
            var nextOrder = products.Where(p => p.CategoryId == cat.Id).Select(p => p.DisplayOrder).DefaultIfEmpty(0).Max() + 1;
            var shade = 0;
            foreach (var spec in group)
            {
                var color = Shade(cat.Color, shade++);
                var product = products.FirstOrDefault(p => p.Name.Equals(spec.Name, StringComparison.OrdinalIgnoreCase));
                if (product is null)
                {
                    product = new Product
                    {
                        Name = spec.Name, Price = spec.Price, VatRate = spec.Vat, Category = cat, Color = color,
                        PreparationMinutes = spec.Prep, DisplayOrder = nextOrder++,
                        // A few items are temporarily sold out so the "unavailable" state shows up in the UI.
                        IsAvailable = _rng.Next(40) != 0,
                    };
                    _db.Products.Add(product);
                    products.Add(product);
                    prodAdded++;
                }

                // Fill blanks on pre-existing rows too, without overwriting anything someone edited.
                product.Description ??= spec.Description;
                product.Ingredients ??= spec.Ingredients;
                if (product.ImageUrl is null || IsPlainPlaceholder(product.ImageUrl))
                    product.ImageUrl = FoodImg(spec.Emoji, product.Color);
                if (product.Allergens.Count == 0 && spec.Allergens.Length > 0)
                    foreach (var code in spec.Allergens.Split(','))
                        product.Allergens.Add(allergenByCode[code]);
            }
        }
        // Pre-existing products that aren't in the demo menu still get a picture.
        foreach (var product in products.Where(p => p.ImageUrl is null || IsPlainPlaceholder(p.ImageUrl)))
        {
            var cat = cats.First(c => c.Id == product.CategoryId || c == product.Category);
            product.ImageUrl = FoodImg(cat.Icon ?? "🍽️", product.Color);
        }

        await _db.SaveChangesAsync(ct);
        if (catsAdded + prodAdded > 0)
            _log.LogInformation("Demo content: added {Cats} categories and {Prods} products.", catsAdded, prodAdded);
    }

    // ------------------------------------------------------------------ extras

    private sealed record ExtraSpec(string Name, decimal Price, string Emoji, string Color, string[] Categories);

    private static readonly ExtraSpec[] Extras =
    {
        new("Extra cheese", 1.50m, "🧀", "#facc15", new[] { "Pizzas", "Pasta", "Burgers", "Kids Menu" }),
        new("Buffalo mozzarella", 2.50m, "🧀", "#f5f5f4", new[] { "Pizzas", "Salads" }),
        new("Burrata", 3.50m, "🧀", "#e7e5e4", new[] { "Pizzas", "Salads" }),
        new("Pepperoni", 1.80m, "🍕", "#dc2626", new[] { "Pizzas" }),
        new("Ham", 1.50m, "🍖", "#fda4af", new[] { "Pizzas", "Kids Menu" }),
        new("Prosciutto", 2.50m, "🥓", "#f43f5e", new[] { "Pizzas", "Salads" }),
        new("Bacon", 1.80m, "🥓", "#b91c1c", new[] { "Pizzas", "Burgers", "Pasta" }),
        new("Mushrooms", 1.20m, "🍄", "#a8a29e", new[] { "Pizzas", "Pasta", "Risotto", "Burgers" }),
        new("Black olives", 1.00m, "🫒", "#1c1917", new[] { "Pizzas", "Salads" }),
        new("Red onion", 0.80m, "🧅", "#a21caf", new[] { "Pizzas", "Burgers", "Salads" }),
        new("Jalapeños", 1.00m, "🌶️", "#16a34a", new[] { "Pizzas", "Burgers", "Starters" }),
        new("Rocket", 1.00m, "🥬", "#15803d", new[] { "Pizzas", "Burgers" }),
        new("Cherry tomatoes", 1.00m, "🍅", "#ef4444", new[] { "Pizzas", "Salads" }),
        new("Anchovies", 1.80m, "🐟", "#0e7490", new[] { "Pizzas", "Salads" }),
        new("Truffle oil", 2.50m, "🫒", "#854d0e", new[] { "Pizzas", "Risotto", "Pasta" }),
        new("Gluten-free base", 2.00m, "🌾", "#ca8a04", new[] { "Pizzas" }),
        new("Stuffed crust", 2.50m, "🧀", "#f59e0b", new[] { "Pizzas" }),
        new("Extra sauce", 0.80m, "🥫", "#b91c1c", new[] { "Pizzas", "Pasta", "Burgers", "Starters" }),
        new("Extra patty", 3.50m, "🍔", "#78350f", new[] { "Burgers" }),
        new("Cheddar", 1.20m, "🧀", "#f97316", new[] { "Burgers", "Starters" }),
        new("Fried egg", 1.20m, "🍳", "#fde047", new[] { "Burgers", "Pizzas", "Meat" }),
        new("Caramelised onion", 1.00m, "🧅", "#92400e", new[] { "Burgers" }),
        new("Avocado", 1.80m, "🥑", "#65a30d", new[] { "Burgers", "Salads" }),
        new("Gluten-free bun", 1.50m, "🍞", "#d97706", new[] { "Burgers" }),
        new("Sweet potato fries", 2.00m, "🍠", "#ea580c", new[] { "Burgers", "Meat" }),
        new("Parmesan", 1.00m, "🧀", "#fef3c7", new[] { "Pasta", "Risotto", "Salads", "Soups" }),
        new("Chilli flakes", 0.50m, "🌶️", "#dc2626", new[] { "Pasta", "Pizzas" }),
        new("Grilled chicken", 2.80m, "🍗", "#d97706", new[] { "Pasta", "Salads" }),
        new("King prawns", 3.90m, "🍤", "#fb923c", new[] { "Pasta", "Risotto", "Salads" }),
        new("Gluten-free pasta", 1.50m, "🍝", "#eab308", new[] { "Pasta" }),
        new("Goat cheese", 2.00m, "🧀", "#f5f5f4", new[] { "Salads" }),
        new("Walnuts", 1.20m, "🌰", "#78350f", new[] { "Salads", "Desserts" }),
        new("Pepper sauce", 2.00m, "🫙", "#57534e", new[] { "Meat" }),
        new("Blue cheese sauce", 2.20m, "🧀", "#60a5fa", new[] { "Meat", "Burgers" }),
        new("Chimichurri", 1.50m, "🌿", "#16a34a", new[] { "Meat", "Fish & Seafood" }),
        new("Side of fries", 3.00m, "🍟", "#facc15", new[] { "Meat", "Fish & Seafood", "Kids Menu" }),
        new("Grilled vegetables", 3.50m, "🥦", "#22c55e", new[] { "Meat", "Fish & Seafood" }),
        new("Oat milk", 0.40m, "🥛", "#e7e5e4", new[] { "Coffee & Tea" }),
        new("Soy milk", 0.40m, "🥛", "#fef9c3", new[] { "Coffee & Tea" }),
        new("Extra shot", 0.60m, "☕", "#78350f", new[] { "Coffee & Tea", "Cocktails" }),
        new("Whipped cream", 0.80m, "🍦", "#fdf2f8", new[] { "Coffee & Tea", "Desserts", "Ice Cream" }),
        new("Premium spirit", 3.00m, "🥃", "#b45309", new[] { "Cocktails" }),
        new("Double measure", 4.00m, "🥃", "#92400e", new[] { "Cocktails" }),
        new("Chocolate sauce", 0.80m, "🍫", "#451a03", new[] { "Desserts", "Ice Cream" }),
        new("Salted caramel", 0.90m, "🍯", "#d97706", new[] { "Desserts", "Ice Cream" }),
        new("Extra scoop", 1.80m, "🍨", "#f9a8d4", new[] { "Ice Cream", "Desserts" }),
        new("Waffle cone", 0.60m, "🍦", "#fbbf24", new[] { "Ice Cream" }),
    };

    /// <summary>Per-product exceptions to a category-wide extra ("no pepperoni on the vegetarian pizza").</summary>
    private static readonly (string Extra, string Product)[] ExtraExceptions =
    {
        ("Pepperoni", "Vegetariana"), ("Ham", "Vegetariana"), ("Prosciutto", "Vegetariana"), ("Bacon", "Vegetariana"),
        ("Pepperoni", "Marinara"), ("Extra cheese", "Marinara"), ("Anchovies", "Four Cheese"),
        ("Bacon", "Veggie Burger"), ("Extra patty", "Veggie Burger"), ("Cheddar", "Veggie Burger"),
        ("Stuffed crust", "Calzone"), ("Gluten-free base", "Calzone"),
        ("King prawns", "Seafood Linguine"), ("King prawns", "Seafood Risotto"),
    };

    private async Task SeedExtrasAsync(CancellationToken ct)
    {
        var extras = await _db.Extras.Include(e => e.Categories).Include(e => e.ExcludedProducts).ToListAsync(ct);
        var cats = await _db.Categories.ToListAsync(ct);
        var added = 0;
        foreach (var spec in Extras)
        {
            var extra = extras.FirstOrDefault(e => e.Name.Equals(spec.Name, StringComparison.OrdinalIgnoreCase));
            if (extra is null)
            {
                extra = new Extra { Name = spec.Name, Price = spec.Price };
                _db.Extras.Add(extra);
                extras.Add(extra);
                added++;
            }
            extra.ImageUrl ??= FoodImg(spec.Emoji, spec.Color);
            foreach (var catName in spec.Categories)
            {
                var cat = cats.FirstOrDefault(c => c.Name.Equals(catName, StringComparison.OrdinalIgnoreCase));
                if (cat is not null && !extra.Categories.Contains(cat)) extra.Categories.Add(cat);
            }
        }

        var products = await _db.Products.ToListAsync(ct);
        foreach (var (extraName, productName) in ExtraExceptions)
        {
            var extra = extras.FirstOrDefault(e => e.Name.Equals(extraName, StringComparison.OrdinalIgnoreCase));
            var product = products.FirstOrDefault(p => p.Name.Equals(productName, StringComparison.OrdinalIgnoreCase));
            if (extra is not null && product is not null && !extra.ExcludedProducts.Contains(product))
                extra.ExcludedProducts.Add(product);
        }

        await _db.SaveChangesAsync(ct);
        if (added > 0) _log.LogInformation("Demo content: added {Count} extras.", added);
    }

    // ------------------------------------------------------------------ floor plan

    private sealed record TableSpec(string Name, int Seats, TableShape Shape, double X, double Y, double W, double H,
        string Zone, string? Color = null, double Rotation = 0);

    private sealed record DecorSpec(FloorDecorType Type, double X, double Y, double W, double H, double Rotation = 0);

    // Whole restaurant, in spec coordinates about 2225x1345 (FloorSpacing then spreads it to roughly
    // 2560x1550 on the canvas): main hall top-left with the bar below it, terrace and garden across
    // the top, a guest corridor under them leading to the private room, lounge, chef's table and
    // wine cellar in the middle band, street terrace and event hall along the bottom with the games
    // room behind the event hall, and a service corridor to the back of house (kitchen, restrooms,
    // stairs) on the right.
    // Table pitch (~125px) leaves room for the chairs drawn around each table (see ChairStyles in
    // Tables.razor), so no two tables' chairs overlap.
    private const string Wood = "#c08a52";
    private const string Walnut = "#5b3a26";
    private const string Violet = "#7c3aed";
    private const string Amber = "#b45309";
    private const string Olive = "#4d7c0f";
    private const string Graphite = "#374151";
    private const string Burgundy = "#7f1d1d";
    private const string Slate = "#475569";
    private const string Rose = "#be185d";
    private const string Orange = "#c2410c";

    // Everything is shifted down a little so the floor map's top-left overlay button doesn't
    // cover the first zone's label.
    private const double FloorOffsetY = 28;

    // Spreads the whole plan out from the spec coordinates below, for more room between tables:
    // positions (and the length of walls, counters and zones) scale up, while tables, chairs,
    // doors and plants keep their size, so the gaps between them grow.
    private const double FloorSpacing = 1.15;

    private static readonly TableSpec[] FloorTables =
    {
        // Main hall — 4 columns x 4 rows. T3/T4 sit side by side (they can be joined as a group).
        new("T1", 4, TableShape.Square, 50, 55, 70, 70, "Main hall"),
        new("T2", 4, TableShape.Square, 178, 55, 70, 70, "Main hall"),
        new("T4", 4, TableShape.Square, 306, 55, 70, 70, "Main hall"),
        new("T3", 6, TableShape.Rectangular, 376, 55, 105, 70, "Main hall"),
        new("T5", 4, TableShape.Square, 50, 173, 70, 70, "Main hall"),
        new("T7", 4, TableShape.Square, 178, 173, 70, 70, "Main hall", Rotation: 45),
        new("T8", 4, TableShape.Round, 306, 173, 70, 70, "Main hall"),
        new("T6", 6, TableShape.Rectangular, 434, 173, 105, 70, "Main hall"),
        new("T9", 4, TableShape.Square, 50, 291, 70, 70, "Main hall"),
        new("T10", 4, TableShape.Square, 178, 291, 70, 70, "Main hall"),
        new("T11", 4, TableShape.Round, 306, 291, 70, 70, "Main hall"),
        new("T12", 4, TableShape.Square, 434, 291, 70, 70, "Main hall"),
        new("T13", 4, TableShape.Square, 50, 409, 70, 70, "Main hall"),
        new("T14", 4, TableShape.Square, 178, 409, 70, 70, "Main hall"),
        // Terrace — 5 columns x 3 rows, wooden tables, 6-seaters in the last column.
        new("T15", 2, TableShape.Round, 630, 55, 60, 60, "Terrace", Wood),
        new("T16", 4, TableShape.Square, 750, 50, 70, 70, "Terrace", Wood),
        new("T17", 4, TableShape.Square, 875, 50, 70, 70, "Terrace", Wood),
        new("T18", 2, TableShape.Round, 1005, 55, 60, 60, "Terrace", Wood),
        new("T20", 6, TableShape.Rectangular, 1120, 50, 105, 70, "Terrace", Wood),
        new("T19", 4, TableShape.Square, 625, 165, 70, 70, "Terrace", Wood),
        new("T21", 4, TableShape.Square, 750, 165, 70, 70, "Terrace", Wood),
        new("T22", 2, TableShape.Round, 880, 170, 60, 60, "Terrace", Wood),
        new("T23", 4, TableShape.Square, 1000, 165, 70, 70, "Terrace", Wood),
        new("T25", 6, TableShape.Rectangular, 1120, 165, 105, 70, "Terrace", Wood),
        new("T24", 4, TableShape.Square, 625, 280, 70, 70, "Terrace", Wood),
        new("T26", 2, TableShape.Round, 755, 285, 60, 60, "Terrace", Wood),
        new("T27", 4, TableShape.Square, 875, 280, 70, 70, "Terrace", Wood),
        new("T28", 2, TableShape.Round, 1005, 285, 60, 60, "Terrace", Wood),
        new("T29", 4, TableShape.Square, 1135, 280, 70, 70, "Terrace", Wood),
        // Private room — two long banquet tables and a round one.
        new("P1", 8, TableShape.Rectangular, 625, 550, 210, 70, "Private room", Violet),
        new("P3", 6, TableShape.Round, 885, 535, 100, 100, "Private room", Violet),
        new("P2", 10, TableShape.Rectangular, 625, 710, 280, 70, "Private room", Violet),
        // Lounge — four round tables.
        new("L1", 4, TableShape.Round, 1060, 545, 70, 70, "Lounge", Amber),
        new("L2", 4, TableShape.Round, 1160, 545, 70, 70, "Lounge", Amber),
        new("L3", 4, TableShape.Round, 1060, 675, 70, 70, "Lounge", Amber),
        new("L4", 4, TableShape.Round, 1160, 675, 70, 70, "Lounge", Amber),
        // Bar — eight bar tables in a row forming the bar top, two stools each on the guests' side;
        // the counter behind them (decor) is the back bar.
        new("B1", 2, TableShape.BarTable, 40, 665, 56, 38, "Bar", Walnut),
        new("B2", 2, TableShape.BarTable, 99, 665, 56, 38, "Bar", Walnut),
        new("B3", 2, TableShape.BarTable, 158, 665, 56, 38, "Bar", Walnut),
        new("B4", 2, TableShape.BarTable, 217, 665, 56, 38, "Bar", Walnut),
        new("B5", 2, TableShape.BarTable, 276, 665, 56, 38, "Bar", Walnut),
        new("B6", 2, TableShape.BarTable, 335, 665, 56, 38, "Bar", Walnut),
        new("B7", 2, TableShape.BarTable, 394, 665, 56, 38, "Bar", Walnut),
        new("B8", 2, TableShape.BarTable, 453, 665, 56, 38, "Bar", Walnut),
        // Garden — open-air, 5 columns x 3 rows to the right of the terrace.
        new("G1", 4, TableShape.Round, 1300, 50, 70, 70, "Garden", Olive),
        new("G2", 2, TableShape.Round, 1430, 55, 60, 60, "Garden", Olive),
        new("G3", 4, TableShape.Square, 1550, 50, 70, 70, "Garden", Olive),
        new("G4", 2, TableShape.Round, 1680, 55, 60, 60, "Garden", Olive),
        new("G5", 4, TableShape.Round, 1800, 50, 70, 70, "Garden", Olive),
        new("G6", 4, TableShape.Square, 1300, 165, 70, 70, "Garden", Olive),
        new("G7", 4, TableShape.Round, 1425, 165, 70, 70, "Garden", Olive),
        new("G8", 6, TableShape.Oval, 1540, 165, 105, 70, "Garden", Olive),
        new("G9", 4, TableShape.Round, 1690, 165, 70, 70, "Garden", Olive),
        new("G10", 4, TableShape.Square, 1800, 165, 70, 70, "Garden", Olive),
        new("G11", 2, TableShape.Round, 1305, 285, 60, 60, "Garden", Olive),
        new("G12", 4, TableShape.Square, 1425, 280, 70, 70, "Garden", Olive),
        new("G13", 4, TableShape.Round, 1550, 280, 70, 70, "Garden", Olive),
        new("G14", 4, TableShape.Square, 1675, 280, 70, 70, "Garden", Olive),
        new("G15", 2, TableShape.Round, 1805, 285, 60, 60, "Garden", Olive),
        // Chef's table — two rows of stools facing the open kitchen pass.
        new("C1", 2, TableShape.Round, 1290, 580, 48, 48, "Chef's table", Graphite),
        new("C2", 2, TableShape.Round, 1345, 580, 48, 48, "Chef's table", Graphite),
        new("C3", 2, TableShape.Round, 1400, 580, 48, 48, "Chef's table", Graphite),
        new("C4", 2, TableShape.Round, 1455, 580, 48, 48, "Chef's table", Graphite),
        new("C5", 2, TableShape.Round, 1290, 700, 48, 48, "Chef's table", Graphite),
        new("C6", 2, TableShape.Round, 1345, 700, 48, 48, "Chef's table", Graphite),
        new("C7", 2, TableShape.Round, 1400, 700, 48, 48, "Chef's table", Graphite),
        new("C8", 2, TableShape.Round, 1455, 700, 48, 48, "Chef's table", Graphite),
        // Wine cellar — high tables between the barrels.
        new("W1", 2, TableShape.Round, 1590, 540, 60, 60, "Wine cellar", Burgundy),
        new("W2", 4, TableShape.Square, 1700, 535, 70, 70, "Wine cellar", Burgundy),
        new("W3", 2, TableShape.Round, 1815, 540, 60, 60, "Wine cellar", Burgundy),
        new("W4", 4, TableShape.Square, 1585, 675, 70, 70, "Wine cellar", Burgundy),
        new("W5", 2, TableShape.Round, 1705, 680, 60, 60, "Wine cellar", Burgundy),
        new("W6", 4, TableShape.Square, 1810, 675, 70, 70, "Wine cellar", Burgundy),
        // Street terrace — sidewalk tables outside the main entrance (gap left for the door).
        new("S1", 2, TableShape.Round, 45, 900, 60, 60, "Street terrace", Slate),
        new("S2", 4, TableShape.Square, 150, 895, 70, 70, "Street terrace", Slate),
        new("S3", 2, TableShape.Round, 360, 900, 60, 60, "Street terrace", Slate),
        new("S4", 4, TableShape.Square, 465, 895, 70, 70, "Street terrace", Slate),
        new("S5", 2, TableShape.Round, 590, 900, 60, 60, "Street terrace", Slate),
        new("S6", 4, TableShape.Square, 695, 895, 70, 70, "Street terrace", Slate),
        new("S7", 2, TableShape.Round, 820, 900, 60, 60, "Street terrace", Slate),
        new("S8", 4, TableShape.Square, 925, 895, 70, 70, "Street terrace", Slate),
        new("S9", 2, TableShape.Round, 1050, 900, 60, 60, "Street terrace", Slate),
        new("S10", 4, TableShape.Square, 1150, 895, 70, 70, "Street terrace", Slate),
        // Event hall — banquet tables for groups and celebrations.
        new("E1", 12, TableShape.Rectangular, 1295, 895, 330, 70, "Event hall", Rose),
        new("E2", 8, TableShape.Rectangular, 1680, 895, 190, 70, "Event hall", Rose),
        // Games room — two board-game tables between the pool/air-hockey and the arcades.
        new("J1", 4, TableShape.Round, 1650, 1240, 60, 60, "Games room", Orange),
        new("J2", 4, TableShape.Round, 1755, 1240, 60, 60, "Games room", Orange),
    };

    private static readonly (string Name, double X, double Y, double W, double H, string Color)[] FloorZoneSpecs =
    {
        ("Main hall", 20, 20, 545, 570, "#6366f1"),
        ("Terrace", 590, 20, 662, 385, "#22c55e"),
        ("Bar", 20, 600, 545, 225, "#14b8a6"),
        ("Private room", 590, 510, 420, 315, "#a855f7"),
        ("Lounge", 1025, 510, 225, 315, "#f59e0b"),
        ("Garden", 1270, 20, 625, 385, "#65a30d"),
        ("Chef's table", 1265, 510, 262, 315, "#64748b"),
        ("Wine cellar", 1542, 510, 353, 315, "#be123c"),
        ("Street terrace", 20, 852, 1230, 170, "#0ea5e9"),
        ("Event hall", 1265, 852, 630, 170, "#ec4899"),
        ("Games room", 1265, 1040, 950, 290, "#f97316"),
    };

    private static readonly DecorSpec[] FloorDecorSpecs =
    {
        // ---- Circulation. A guest corridor runs between the terrace/garden and the middle rooms,
        // from the main hall to the back of house, with a door into every room along it; a staff
        // service corridor runs down behind the dining rooms to the kitchen, restrooms and stairs.
        // Both come first so they sit under everything else.
        new(FloorDecorType.Corridor, 582, 418, 1318, 80),
        new(FloorDecorType.Corridor, 1910, 16, 70, 1009),
        // ---- Building shell. Walls are stored at their final size (vertical ones are just tall
        // and thin) rather than rotated, so the saved box is exactly what's drawn. Glass walls
        // separate the open-air terrace and garden from the corridor.
        new(FloorDecorType.Wall, 10, 6, 572, 10),        // top of main hall
        new(FloorDecorType.Wall, 10, 6, 10, 834),        // left
        new(FloorDecorType.Wall, 10, 830, 1900, 10),     // front of the building (onto the street)
        new(FloorDecorType.GlassWall, 572, 6, 10, 402),  // main hall | terrace
        new(FloorDecorType.Wall, 572, 408, 10, 432),     // main hall + bar | corridor + private room
        new(FloorDecorType.GlassWall, 582, 408, 678, 10),// terrace | corridor
        new(FloorDecorType.GlassWall, 1260, 408, 640, 10),// garden | corridor
        new(FloorDecorType.Wall, 582, 498, 1318, 10),    // corridor | middle rooms
        new(FloorDecorType.Wall, 1015, 508, 10, 322),    // private room | lounge
        new(FloorDecorType.Wall, 1250, 508, 10, 322),    // lounge | chef's table
        new(FloorDecorType.Wall, 1532, 508, 10, 322),    // chef's table | cellar
        new(FloorDecorType.Wall, 1900, 6, 10, 1030),     // dining | service corridor
        new(FloorDecorType.Wall, 1900, 6, 325, 10),      // back of house: top
        new(FloorDecorType.Wall, 1980, 6, 10, 1030),     // service corridor | back rooms
        new(FloorDecorType.Wall, 2215, 6, 10, 1340),     // back of house + games room: right
        new(FloorDecorType.Wall, 1990, 645, 225, 10),    // kitchen | restrooms
        new(FloorDecorType.Wall, 1990, 832, 225, 10),    // restrooms | stairs
        new(FloorDecorType.Wall, 1255, 840, 10, 192),    // street terrace | event hall
        new(FloorDecorType.Wall, 1255, 1025, 970, 10),   // event hall + back of house | games room
        new(FloorDecorType.Wall, 1255, 1025, 10, 320),   // games room: left
        new(FloorDecorType.Wall, 1255, 1335, 970, 10),   // games room: bottom
        // ---- Windows and doors sit on top of the walls; vertical doors are rotated 90°.
        new(FloorDecorType.Window, 70, 4, 140, 14),
        new(FloorDecorType.Window, 330, 4, 140, 14),
        new(FloorDecorType.Window, 8, 150, 14, 120),
        new(FloorDecorType.Window, 8, 640, 14, 110),
        new(FloorDecorType.Window, 700, 828, 120, 14),
        new(FloorDecorType.Window, 1080, 828, 120, 14),
        new(FloorDecorType.Window, 1320, 828, 140, 14),
        new(FloorDecorType.Window, 1640, 828, 160, 14),
        new(FloorDecorType.Window, 1420, 1333, 160, 14),
        new(FloorDecorType.Window, 1860, 1333, 160, 14),
        new(FloorDecorType.Window, 2213, 180, 14, 140),
        new(FloorDecorType.DoubleDoor, 220, 823, 110, 16, 180), // main entrance, both leaves swinging inwards
        new(FloorDecorType.Door, 547, 232, 60, 16, 90),  // main hall -> terrace
        new(FloorDecorType.Door, 547, 450, 60, 16, 90),  // main hall -> corridor
        new(FloorDecorType.Door, 547, 712, 60, 16, 90),  // bar -> private room
        new(FloorDecorType.SlidingDoor, 745, 406, 90, 14),  // terrace -> corridor (glazed)
        new(FloorDecorType.SlidingDoor, 1095, 406, 90, 14), // terrace -> corridor (glazed)
        new(FloorDecorType.SlidingDoor, 1675, 406, 90, 14), // garden -> corridor (glazed)
        new(FloorDecorType.Door, 700, 495, 60, 16),      // corridor -> private room
        new(FloorDecorType.Door, 1110, 495, 60, 16),     // corridor -> lounge
        new(FloorDecorType.Door, 1360, 495, 60, 16),     // corridor -> chef's table
        new(FloorDecorType.Door, 1690, 495, 60, 16),     // corridor -> cellar
        new(FloorDecorType.Door, 1225, 662, 60, 16, 90), // lounge -> chef's table
        new(FloorDecorType.Door, 1507, 662, 60, 16, 90), // chef's table -> cellar
        new(FloorDecorType.Door, 1480, 827, 60, 16),     // chef's table -> event hall
        new(FloorDecorType.Door, 1875, 450, 60, 16, 90), // corridor -> service corridor
        new(FloorDecorType.Door, 1875, 922, 60, 16, 90), // event hall -> service corridor
        new(FloorDecorType.Door, 1615, 1022, 60, 16),    // event hall -> games room
        new(FloorDecorType.Door, 1955, 292, 60, 16, 90), // service corridor -> kitchen
        new(FloorDecorType.Door, 1955, 737, 60, 16, 90), // service corridor -> restrooms
        new(FloorDecorType.Door, 1955, 922, 60, 16, 90), // service corridor -> stairs
        // ---- Back of house.
        new(FloorDecorType.Kitchen, 1990, 16, 225, 629),
        new(FloorDecorType.Restrooms, 1990, 655, 225, 177),
        new(FloorDecorType.Stairs, 2058, 852, 90, 164),
        // ---- Main hall and bar.
        new(FloorDecorType.BarCounter, 35, 730, 470, 46),
        new(FloorDecorType.HostStand, 336, 788, 46, 30),
        new(FloorDecorType.SmallTree, 302, 412, 76, 76),
        new(FloorDecorType.Column, 452, 428, 36, 36),
        new(FloorDecorType.PottedPlant, 505, 525, 50, 50),
        new(FloorDecorType.SmallPlant, 518, 735, 36, 36),
        new(FloorDecorType.HangingPlant, 26, 780, 40, 40),
        new(FloorDecorType.CoatRack, 150, 790, 36, 36),
        // ---- Main hall: a waiting corner with a sofa, armchair and lamp, and a waiter station.
        new(FloorDecorType.Sofa, 60, 528, 140, 46),
        new(FloorDecorType.Armchair, 214, 528, 46, 46),
        new(FloorDecorType.FloorLamp, 26, 534, 32, 32),
        new(FloorDecorType.WaiterStation, 380, 534, 76, 36),
        // ---- Terrace: planter along the glass.
        new(FloorDecorType.Planter, 596, 380, 646, 20),
        // ---- Garden: hedge against the terrace, planter along the glass, trees in the corners.
        new(FloorDecorType.Planter, 1254, 26, 16, 346),
        new(FloorDecorType.Planter, 1276, 380, 616, 20),
        new(FloorDecorType.SmallTree, 1858, 24, 40, 40),
        // ---- Corridor: plants between the doors.
        new(FloorDecorType.Fern, 905, 437, 40, 40),
        new(FloorDecorType.Fern, 1515, 437, 40, 40),
        // ---- Private room: rugs under the banquet tables.
        new(FloorDecorType.Rug, 600, 530, 404, 136),
        new(FloorDecorType.Rug, 604, 684, 322, 122),
        new(FloorDecorType.PottedPlant, 952, 768, 50, 50),
        // ---- Lounge: one rug under the four tables.
        new(FloorDecorType.Rug, 1034, 525, 208, 290),
        new(FloorDecorType.Fern, 1030, 780, 42, 42),
        new(FloorDecorType.HangingPlant, 1200, 780, 42, 42),
        // ---- Chef's table: the kitchen pass and the chef's counter.
        new(FloorDecorType.BarCounter, 1285, 532, 225, 34),
        new(FloorDecorType.BarCounter, 1285, 770, 225, 34),
        // ---- Wine cellar: racks along the walls, barrels between the tables.
        new(FloorDecorType.WineRack, 1560, 784, 300, 26),
        new(FloorDecorType.WineRack, 1866, 520, 24, 240),
        new(FloorDecorType.Column, 1640, 740, 30, 30),
        new(FloorDecorType.Column, 1790, 600, 30, 30),
        // ---- Street terrace: planters along the kerb (gap left at the entrance), trees by the door.
        new(FloorDecorType.Planter, 30, 998, 190, 18),
        new(FloorDecorType.Planter, 340, 998, 900, 18),
        new(FloorDecorType.SmallTree, 246, 866, 46, 46),
        new(FloorDecorType.SmallTree, 286, 952, 42, 42),
        new(FloorDecorType.Parasol, 30, 885, 90, 90),
        new(FloorDecorType.Parasol, 575, 885, 90, 90),
        new(FloorDecorType.Parasol, 1035, 885, 90, 90),
        // ---- Event hall: banquettes along the back wall.
        new(FloorDecorType.Banquette, 1300, 995, 300, 26),
        new(FloorDecorType.Banquette, 1690, 995, 180, 26),
        new(FloorDecorType.PottedPlant, 1268, 975, 40, 40),
        // ---- Games room: pool and table tennis either side of the entrance from the event hall,
        // foosball and air hockey below, arcades against the back wall, a dartboard and jukebox on
        // the right wall, and bean bags to lounge on.
        new(FloorDecorType.Carpet, 1265, 1035, 950, 300),
        new(FloorDecorType.PoolTable, 1305, 1090, 200, 110),
        new(FloorDecorType.CueRack, 1345, 1037, 120, 22),
        new(FloorDecorType.PingPong, 1745, 1065, 180, 100),
        new(FloorDecorType.Foosball, 1300, 1240, 130, 70),
        new(FloorDecorType.AirHockey, 1455, 1225, 140, 78),
        new(FloorDecorType.ArcadeMachine, 1990, 1262, 60, 56),
        new(FloorDecorType.ArcadeMachine, 2058, 1262, 60, 56),
        new(FloorDecorType.ArcadeMachine, 2126, 1262, 60, 56),
        new(FloorDecorType.Dartboard, 2160, 1135, 44, 54),
        new(FloorDecorType.Jukebox, 2140, 1055, 62, 48),
        new(FloorDecorType.BeanBag, 1868, 1240, 44, 44),
        new(FloorDecorType.BeanBag, 1918, 1276, 40, 40),
        new(FloorDecorType.FloorLamp, 1960, 1060, 32, 32),
        new(FloorDecorType.SmallTree, 1270, 1290, 42, 42),
    };

    /// <summary>
    /// Lays out the whole floor plan: repositions existing tables by name, adds the missing ones and
    /// rewrites zones/decor in place — nothing is deleted. Runs on a fresh database, and once more on
    /// a database still holding an earlier demo layout (T2 untouched at its pre-spacing spot, no
    /// kitchen, corridors, sliding doors or games room drawn yet, or the bar still made of round stools) so it picks up the current design; after that it never overwrites what
    /// was moved in the editor.
    /// </summary>
    private async Task SeedFloorAsync(CancellationToken ct)
    {
        var laidOut = await _db.Tables.AnyAsync(t => t.Name == "E2", ct);
        var previousLayout = await _db.Tables.AnyAsync(t => t.Name == "T2" && t.PositionX == 178 && t.PositionY == 55 + FloorOffsetY, ct)
                             || !await _db.FloorDecors.AnyAsync(d => d.Type == FloorDecorType.Kitchen, ct)
                             || await _db.Tables.AnyAsync(t => t.Name == "B1" && t.Shape == TableShape.Round && t.Width == 48, ct)
                             || !await _db.FloorDecors.AnyAsync(d => d.Type == FloorDecorType.Corridor, ct)
                             || !await _db.FloorDecors.AnyAsync(d => d.Type == FloorDecorType.SlidingDoor, ct)
                             || !await _db.FloorDecors.AnyAsync(d => d.Type == FloorDecorType.CueRack, ct);
        if (laidOut && !previousLayout) return;
        _log.LogInformation("Demo content: laying out the floor plan.");

        // Scale a fixed-size item's centre, so it stays centred in its (now larger) slot.
        static double Centre(double pos, double size) => (pos + size / 2) * FloorSpacing - size / 2;

        var tables = await _db.Tables.ToListAsync(ct);
        foreach (var spec in FloorTables)
        {
            var t = tables.FirstOrDefault(x => x.Name == spec.Name);
            if (t is null)
            {
                t = new RestaurantTable { Name = spec.Name };
                _db.Tables.Add(t);
                tables.Add(t);
            }
            t.Seats = spec.Seats;
            t.Shape = spec.Shape;
            t.PositionX = Centre(spec.X, spec.W);
            t.PositionY = Centre(spec.Y, spec.H) + FloorOffsetY;
            t.Width = spec.W;
            t.Height = spec.H;
            t.Rotation = spec.Rotation;
            t.Zone = spec.Zone;
            t.Color = spec.Color;
        }

        // Joined tables must stay butted edge to edge in one row (see JoinTablesAsync), which
        // centre-scaling each member on its own would pull apart.
        foreach (var group in tables.Where(t => t.GroupId is not null).GroupBy(t => t.GroupId))
        {
            var members = group.OrderBy(t => t.PositionX).ToList();
            for (var i = 1; i < members.Count; i++)
            {
                members[i].PositionX = members[i - 1].PositionX + members[i - 1].Width;
                members[i].PositionY = members[0].PositionY;
            }
        }
        foreach (var t in tables)
        {
            // Joined tables restore these on "separate" — point them at the new spot too.
            if (t.PreJoinPositionX is not null) t.PreJoinPositionX = t.PositionX;
            if (t.PreJoinPositionY is not null) t.PreJoinPositionY = t.PositionY;
            if (t.PreJoinHeight is not null) t.PreJoinHeight = t.Height;
            if (t.PreJoinRotation is not null) t.PreJoinRotation = t.Rotation;
        }

        var zones = await _db.FloorZones.ToListAsync(ct);
        foreach (var (name, x, y, w, h, color) in FloorZoneSpecs)
        {
            var z = zones.FirstOrDefault(v => v.Name == name);
            if (z is null)
            {
                z = new FloorZone { Name = name };
                _db.FloorZones.Add(z);
            }
            (z.PositionX, z.PositionY, z.Width, z.Height, z.Color) =
                (x * FloorSpacing, y * FloorSpacing + FloorOffsetY, w * FloorSpacing, h * FloorSpacing, color);
        }

        // Reuse existing decor rows for the new pieces, adding rows only for the extra ones.
        var decor = await _db.FloorDecors.OrderBy(d => d.Id).ToListAsync(ct);
        for (var i = 0; i < FloorDecorSpecs.Length; i++)
        {
            var spec = FloorDecorSpecs[i];
            var d = i < decor.Count ? decor[i] : _db.FloorDecors.Add(new FloorDecor()).Entity;
            d.Type = spec.Type;
            // Rooms, corridors and rugs grow with the rooms around them; walls, glazing, counters, racks,
            // planters and benches stretch along their length (keeping their thickness) so they
            // still meet at the corners; everything else keeps its size and is re-centred.
            var fills = spec.Type is FloorDecorType.Kitchen or FloorDecorType.Restrooms or FloorDecorType.Rug or FloorDecorType.Corridor or FloorDecorType.Carpet;
            var stretches = spec.Type is FloorDecorType.Wall or FloorDecorType.Window or FloorDecorType.GlassWall
                or FloorDecorType.BarCounter or FloorDecorType.WineRack or FloorDecorType.Planter or FloorDecorType.Banquette;
            var horizontal = spec.W >= spec.H;
            double x, y, w = spec.W, h = spec.H;
            if (fills) { x = spec.X * FloorSpacing; y = spec.Y * FloorSpacing; w = spec.W * FloorSpacing; h = spec.H * FloorSpacing; }
            else if (stretches && horizontal) { x = spec.X * FloorSpacing; w = spec.W * FloorSpacing; y = Centre(spec.Y, spec.H); }
            else if (stretches) { y = spec.Y * FloorSpacing; h = spec.H * FloorSpacing; x = Centre(spec.X, spec.W); }
            else { x = Centre(spec.X, spec.W); y = Centre(spec.Y, spec.H); }
            (d.PositionX, d.PositionY, d.Width, d.Height, d.Rotation) = (x, y + FloorOffsetY, w, h, spec.Rotation);
            d.IsLocked = true;
        }

        var settings = await _db.AppSettings.FirstOrDefaultAsync(ct);
        if (settings is not null && settings.FloorTexture == "grid") settings.FloorTexture = "wood";

        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ customers & reservations

    private static readonly string[] FirstNames =
    {
        "Laura", "Javier", "Carmen", "Miguel", "Isabel", "Alejandro", "Marta", "Daniel", "Paula", "Adrián",
        "Sofía", "Hugo", "Lucía", "Mario", "Julia", "Álvaro", "Elena", "Diego", "Sara", "Iván",
        "Noelia", "Raúl", "Claudia", "Rubén", "Irene", "Óscar", "Nerea", "Marcos", "Andrea", "Jorge",
        "Emma", "Liam", "Olivia", "Noah", "Chloe", "Luca", "Giulia", "Marco", "Francesca", "Pierre",
    };

    private static readonly string[] LastNames =
    {
        "García", "Martínez", "López", "Sánchez", "Pérez", "Gómez", "Martín", "Jiménez", "Ruiz", "Hernández",
        "Díaz", "Moreno", "Muñoz", "Álvarez", "Romero", "Alonso", "Gutiérrez", "Navarro", "Torres", "Domínguez",
        "Rossi", "Bianchi", "Smith", "Johnson", "Dubois", "Müller",
    };

    private static readonly string[] ReservationComments =
    {
        "Birthday celebration 🎂", "Window table if possible", "Allergic to nuts", "Coeliac — gluten-free options please",
        "Needs wheelchair access", "Anniversary dinner", "Business meeting, quiet table", "Coming with a dog",
        "Terrace preferred", "Vegetarian group", "Will arrive 15 minutes late", "Surprise cake — bring at dessert",
    };

    private static readonly string[] ReservationColors =
        { "#6366f1", "#0ea5e9", "#22c55e", "#f59e0b", "#ef4444", "#ec4899", "#8b5cf6", "#14b8a6" };

    private async Task SeedCustomersAndReservationsAsync(CancellationToken ct)
    {
        if (await _db.Customers.CountAsync(ct) >= 50) return;

        _log.LogInformation("Demo content: adding customers and reservations.");
        var customers = new List<Customer>();
        var usedNames = new HashSet<string>();
        while (customers.Count < 80)
        {
            var first = Pick(FirstNames);
            var last = Pick(LastNames);
            var name = $"{first} {last}";
            if (!usedNames.Add(name)) continue;
            customers.Add(new Customer
            {
                Name = name,
                Phone = $"+34 6{_rng.Next(10)}{_rng.Next(10)} {_rng.Next(100, 999)} {_rng.Next(100, 999)}",
                Email = _rng.Next(4) == 0 ? null : $"{Slug(first)}.{Slug(last)}{_rng.Next(1, 99)}@example.com",
            });
        }
        _db.Customers.AddRange(customers);
        await _db.SaveChangesAsync(ct);

        var tables = await _db.Tables.Where(t => !t.IsArchived).ToListAsync(ct);
        var today = DateTime.UtcNow.Date;
        var lunchSlots = new[] { new TimeOnly(13, 0), new TimeOnly(13, 30), new TimeOnly(14, 0), new TimeOnly(14, 30), new TimeOnly(15, 0) };
        var dinnerSlots = new[] { new TimeOnly(20, 0), new TimeOnly(20, 30), new TimeOnly(21, 0), new TimeOnly(21, 30), new TimeOnly(22, 0) };

        for (var offset = -30; offset <= 21; offset++)
        {
            var date = today.AddDays(offset);
            var weekend = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday;
            var count = _rng.Next(weekend ? 8 : 3, weekend ? 16 : 9);
            if (offset > 10) count /= 2; // fewer bookings further out
            var takenBySlot = new Dictionary<bool, HashSet<int>> { [true] = new(), [false] = new() };

            for (var i = 0; i < count; i++)
            {
                var lunch = _rng.Next(3) == 0;
                var customer = Pick(customers);
                var party = _rng.Next(10) switch { < 4 => 2, < 6 => 4, < 7 => 3, < 8 => 5, < 9 => 6, _ => _rng.Next(7, 11) };
                var status = offset < 0
                    ? (_rng.Next(10) == 0 ? ReservationStatus.Cancelled : ReservationStatus.Finished)
                    : offset == 0 ? (_rng.Next(3) == 0 ? ReservationStatus.Pending : ReservationStatus.Confirmed)
                    : (_rng.Next(2) == 0 ? ReservationStatus.Pending : ReservationStatus.Confirmed);

                var reservation = new Reservation
                {
                    CustomerName = customer.Name,
                    Phone = customer.Phone,
                    Customer = customer,
                    Date = date,
                    Time = Pick(lunch ? lunchSlots : dinnerSlots),
                    DurationMinutes = party > 6 ? 150 : _rng.Next(2) == 0 ? 90 : 120,
                    PartySize = party,
                    ChildrenCount = party >= 4 && _rng.Next(4) == 0 ? _rng.Next(1, 3) : 0,
                    Comments = _rng.Next(3) == 0 ? Pick(ReservationComments) : null,
                    Color = Pick(ReservationColors),
                    Status = status,
                    CreatedAt = date.AddDays(-_rng.Next(1, 15)),
                };
                reservation.HighChairCount = reservation.ChildrenCount > 0 && _rng.Next(2) == 0 ? 1 : 0;

                // Assign a table that fits the party and isn't already booked in the same service.
                if (status != ReservationStatus.Cancelled && offset != 0)
                {
                    var table = tables
                        .Where(t => t.Seats >= party && !takenBySlot[lunch].Contains(t.Id))
                        .OrderBy(t => t.Seats).ThenBy(_ => _rng.Next())
                        .FirstOrDefault();
                    if (table is not null)
                    {
                        reservation.Tables.Add(table);
                        takenBySlot[lunch].Add(table.Id);
                    }
                }
                _db.Reservations.Add(reservation);
            }
        }

        _db.SkipAuditStamp = true;
        await _db.SaveChangesAsync(ct);
        _db.SkipAuditStamp = false;
    }

    // ------------------------------------------------------------------ sales history

    private sealed record MenuItem(Product Product, Category Category, List<Extra> Extras);

    private async Task<List<MenuItem>> LoadMenuAsync(CancellationToken ct)
    {
        var products = await _db.Products.Include(p => p.Category).ThenInclude(c => c.Comments).Include(p => p.Extras)
            .Where(p => p.IsVisible && p.Category.IsVisible).ToListAsync(ct);
        var extras = await _db.Extras.Include(e => e.Categories).Include(e => e.ExcludedProducts).ToListAsync(ct);
        return products.Select(p => new MenuItem(p, p.Category,
            extras.Where(e => (e.Categories.Any(c => c.Id == p.CategoryId) && !e.ExcludedProducts.Any(x => x.Id == p.Id))
                              || p.Extras.Any(x => x.Id == e.Id)).ToList())).ToList();
    }

    private async Task SeedSalesHistoryAsync(CancellationToken ct)
    {
        if (await _db.Orders.AnyAsync(o => o.Number.StartsWith(HistoryOrderPrefix), ct)) return;

        _log.LogInformation("Demo content: generating {Days} days of sales history (this can take a moment).", HistoryDays);
        var menu = await LoadMenuAsync(ct);
        var waiters = await _db.Users.Where(u => u.Role == UserRole.Waiter || u.Role == UserRole.Cashier).ToListAsync(ct);
        var tables = await _db.Tables.Where(t => !t.IsArchived).ToListAsync(ct);

        var drinks = menu.Where(m => m.Category.Kind == CategoryKind.Drink && m.Category.Course != CourseType.Dessert).ToList();
        var coffees = menu.Where(m => m.Category.Kind == CategoryKind.Drink && m.Category.Course == CourseType.Dessert).ToList();
        var starters = menu.Where(m => m.Category.Kind == CategoryKind.Food && m.Category.Course == CourseType.Starter).ToList();
        var mains = menu.Where(m => m.Category.Kind == CategoryKind.Food && m.Category.Course == CourseType.Main).ToList();
        var desserts = menu.Where(m => m.Category.Kind == CategoryKind.Food && m.Category.Course == CourseType.Dessert).ToList();
        // Pizzas sell best at a pizzeria: weight them up.
        mains.AddRange(mains.Where(m => m.Category.Name == "Pizzas").ToList());
        mains.AddRange(mains.Where(m => m.Category.Name == "Pizzas").Take(8).ToList());

        var seq = 1;
        _db.SkipAuditStamp = true;
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        for (var day = HistoryDays; day >= 1; day--)
        {
            var date = DateTime.Today.AddDays(-day);
            var dayFactor = date.DayOfWeek switch
            {
                DayOfWeek.Monday => 0.6, DayOfWeek.Tuesday => 0.7, DayOfWeek.Wednesday => 0.8, DayOfWeek.Thursday => 0.9,
                DayOfWeek.Friday => 1.3, DayOfWeek.Saturday => 1.5, _ => 1.2,
            };
            var bills = (int)Math.Round((18 + _rng.Next(12)) * dayFactor);

            for (var k = 0; k < bills; k++)
            {
                var lunch = _rng.NextDouble() < 0.42;
                // Service hours are wall-clock times at the restaurant; timestamps are stored in UTC.
                var opened = DateTime.SpecifyKind(
                    date.AddMinutes(lunch ? 13 * 60 + _rng.Next(0, 150) : 20 * 60 + _rng.Next(0, 150)),
                    DateTimeKind.Local).ToUniversalTime();
                var party = _rng.Next(10) switch { < 4 => 2, < 6 => 4, < 7 => 1, < 8 => 3, < 9 => 5, _ => 6 };
                var order = new Order
                {
                    Number = $"{HistoryOrderPrefix}{seq:D6}",
                    TableId = Pick(tables).Id,
                    WaiterId = Pick(waiters).Id,
                    Status = OrderStatus.Paid,
                    CreatedAt = opened,
                    ClosedAt = opened.AddMinutes(40 + _rng.Next(0, 50)),
                };
                order.DrinksServedAt = opened.AddMinutes(5);

                void AddLines(List<MenuItem> pool, int count, int minutesIn)
                {
                    for (var n = 0; n < count && pool.Count > 0; n++)
                    {
                        var m = Pick(pool);
                        var existing = order.Items.FirstOrDefault(i => i.ProductId == m.Product.Id && i.Extras.Count == 0);
                        var withExtras = m.Extras.Count > 0 && _rng.Next(6) == 0;
                        if (existing is not null && !withExtras) { existing.Quantity++; continue; }
                        var item = new OrderItem
                        {
                            ProductId = m.Product.Id,
                            Quantity = 1,
                            UnitPrice = m.Product.Price,
                            VatRate = m.Product.VatRate,
                            Status = OrderItemStatus.Delivered,
                            CreatedAt = opened.AddMinutes(minutesIn),
                            Comment = _rng.Next(15) == 0 && m.Category.Comments.Count > 0 ? Pick(m.Category.Comments.ToList()).Text : null,
                        };
                        if (withExtras)
                            foreach (var e in m.Extras.OrderBy(_ => _rng.Next()).Take(_rng.Next(1, 3)))
                                item.Extras.Add(new OrderItemExtra { Name = e.Name, Price = e.Price, ExtraId = e.Id, CreatedAt = item.CreatedAt });
                        order.Items.Add(item);
                    }
                }

                AddLines(drinks, party + _rng.Next(0, party + 1), 2);
                AddLines(starters, Math.Max(1, party / 2 + _rng.Next(-1, 2)), 4);
                AddLines(mains, party, 6);
                if (_rng.NextDouble() < 0.55) AddLines(desserts, _rng.Next(1, party + 1), 45);
                if (_rng.NextDouble() < 0.5) AddLines(coffees, _rng.Next(1, party + 1), 55);
                if (order.Items.Count == 0) continue;

                var total = order.Total;
                var invoice = new Invoice
                {
                    Number = $"{HistoryInvoicePrefix}{seq:D6}",
                    Subtotal = Math.Round(order.Subtotal, 2),
                    VatTotal = Math.Round(order.VatTotal, 2),
                    Total = Math.Round(total, 2),
                    CreatedAt = order.ClosedAt.Value,
                };
                var roll = _rng.Next(20);
                if (roll < 2 && party > 1)
                {
                    // Split bill across cash and card.
                    var cash = Math.Round(invoice.Total * (decimal)(0.3 + _rng.NextDouble() * 0.4), 2);
                    invoice.PaymentMethod = PaymentMethod.Other;
                    invoice.Payments.Add(new Payment { Amount = cash, Method = PaymentMethod.Cash, CreatedAt = invoice.CreatedAt });
                    invoice.Payments.Add(new Payment { Amount = invoice.Total - cash, Method = PaymentMethod.Card, CreatedAt = invoice.CreatedAt });
                }
                else
                {
                    var method = roll < 13 ? PaymentMethod.Card : PaymentMethod.Cash;
                    invoice.PaymentMethod = method;
                    invoice.Payments.Add(new Payment { Amount = invoice.Total, Method = method, CreatedAt = invoice.CreatedAt });
                }
                order.Invoice = invoice;
                _db.Orders.Add(order);
                seq++;
            }

            // Flush a week at a time to keep the change tracker small.
            if (day % 7 == 0 || day == 1)
            {
                _db.ChangeTracker.DetectChanges();
                await _db.SaveChangesAsync(ct);
                _db.ChangeTracker.Clear();
            }
        }
        _db.ChangeTracker.AutoDetectChangesEnabled = true;
        _db.SkipAuditStamp = false;
        _log.LogInformation("Demo content: generated {Count} paid orders.", seq - 1);
    }

    // ------------------------------------------------------------------ suppliers

    private sealed record SupplierSpec(string Name, string Contact, string City, string Notes, bool Active, string[] Categories);

    private static readonly SupplierSpec[] Suppliers =
    {
        new("Northern Waters & Soft Drinks", "Ignacio Vidal", "Bilbao", "Delivers Tuesdays and Fridays before 11:00. Returns empty crates.", true, new[] { "Drinks" }),
        new("La Espiga Craft Brewery", "Marta Olmedo", "Valencia", "Kegs of 30 l. Deposit of €30 per keg.", true, new[] { "Beers" }),
        new("Viña Alta Wine Cellars", "Rodrigo Salas", "Logroño", "Quarterly price list. Minimum order 6 cases.", true, new[] { "Wines" }),
        new("Central Coffee Roasters", "Beatriz Campos", "Madrid", "Beans roasted weekly. Machine maintenance included.", true, new[] { "Coffee & Tea" }),
        new("Polar Artisan Gelato", "Giorgio Ferri", "Barcelona", "Frozen delivery in 5 l tubs. Keep at −18 °C.", true, new[] { "Ice Cream" }),
        new("Dulce Horno Patisserie", "Nuria Castaño", "Madrid", "Fresh desserts delivered daily at 10:00.", true, new[] { "Desserts" }),
        new("Cantabrian Fish & Seafood", "Tomás Herrera", "Santander", "Order by 18:00 for next-day delivery. Cold chain certified.", true, new[] { "Fish & Seafood" }),
        new("Premium Meats Iberia", "Rosa Delgado", "Salamanca", "Aged beef, 30-day minimum. Invoice monthly.", true, new[] { "Meat", "Burgers" }),
        new("Pasta Fresca Mamma Rosa", "Francesca Conti", "Madrid", "Fresh pasta made to order, 48 h notice.", true, new[] { "Pasta", "Risotto" }),
        new("Caseificio Italia Dairy", "Luca Moretti", "Naples (IT)", "Mozzarella and burrata flown in twice a week.", true, new[] { "Pizzas" }),
        new("Huerta Verde Produce", "Pilar Ortega", "Murcia", "Seasonal vegetables and herbs. Organic certified.", true, new[] { "Salads", "Soups", "Starters" }),
        new("CleanPro Hygiene Supplies", "Fernando Gil", "Madrid", "Detergents, gloves and cleaning products. Monthly delivery.", true, Array.Empty<string>()),
        new("EcoPack Packaging", "Silvia Prieto", "Zaragoza", "Compostable pizza boxes and takeaway containers.", true, Array.Empty<string>()),
        new("Frigo Service Maintenance", "Andrés Molina", "Madrid", "Refrigeration and oven maintenance contract. 24 h emergency line.", true, Array.Empty<string>()),
        new("Old Wine Distributors", "Jaime Lorenzo", "Toledo", "Former supplier — contract ended in March.", false, new[] { "Wines" }),
    };

    private async Task SeedSuppliersAsync(string webRootPath, CancellationToken ct)
    {
        var existing = await _db.Suppliers.Select(s => s.Name).ToListAsync(ct);
        var added = new List<Supplier>();
        var n = 0;
        foreach (var spec in Suppliers)
        {
            n++;
            if (existing.Contains(spec.Name)) continue;
            var slug = Slug(spec.Name.Split(' ')[0]);
            var supplier = new Supplier
            {
                Name = spec.Name,
                ContactName = spec.Contact,
                Phone = $"+34 9{_rng.Next(10, 99)} {_rng.Next(100, 999)} {_rng.Next(100, 999)}",
                Email = $"orders@{slug}-demo.example.com",
                TaxId = $"B-{_rng.Next(10000000, 99999999)}",
                Address = $"Industrial Estate {(char)('A' + n % 6)}, Unit {_rng.Next(1, 60)}, {spec.City}",
                Notes = spec.Notes,
                IsActive = spec.Active,
            };
            _db.Suppliers.Add(supplier);
            added.Add(supplier);
        }
        if (added.Count == 0) return;
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Demo content: added {Count} suppliers with documents.", added.Count);

        QuestPDF.Settings.License = LicenseType.Community;
        foreach (var supplier in added)
        {
            var dir = Path.Combine(webRootPath, "uploads", "suppliers", supplier.Id.ToString());
            Directory.CreateDirectory(dir);
            var docs = new List<(string File, string Title, string[] Lines)>
            {
                ("supply-contract-2026.pdf", "Supply agreement 2026", new[]
                {
                    $"Between La Toscana Pizzeria S.L. and {supplier.Name}.",
                    "1. The supplier commits to deliver the goods listed in the current price list.",
                    "2. Payment terms: 30 days from invoice date by bank transfer.",
                    "3. Deliveries must respect the cold chain and food-safety regulations.",
                    "4. This agreement is valid from 1 January to 31 December 2026.",
                    "This is a demo document generated for testing purposes.",
                }),
                ("price-list-2026.pdf", "Price list 2026", PriceListLines(supplier)),
            };
            if (_rng.Next(2) == 0)
                docs.Add(("food-safety-certificate.pdf", "Food safety certificate", new[]
                {
                    $"{supplier.Name} complies with the applicable hygiene and food-safety standards.",
                    $"Certificate no. FS-{_rng.Next(100000, 999999)} — valid until 31/12/2027.",
                    "Demo document — not a real certificate.",
                }));

            foreach (var (file, title, lines) in docs)
            {
                var bytes = Document.Create(c => c.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(t => t.FontSize(11));
                    page.Header().Column(col =>
                    {
                        col.Item().Text(supplier.Name).FontSize(10).FontColor(Colors.Grey.Darken1);
                        col.Item().Text(title).FontSize(22).Bold();
                        col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });
                    page.Content().PaddingTop(16).Column(col =>
                    {
                        col.Spacing(8);
                        foreach (var line in lines) col.Item().Text(line);
                    });
                    page.Footer().AlignRight().Text($"{supplier.Address} · {supplier.Phone}").FontSize(8).FontColor(Colors.Grey.Medium);
                })).GeneratePdf();

                await File.WriteAllBytesAsync(Path.Combine(dir, file), bytes, ct);
                _db.SupplierDocuments.Add(new SupplierDocument
                {
                    SupplierId = supplier.Id,
                    FileName = file,
                    FileUrl = $"/uploads/suppliers/{supplier.Id}/{file}",
                    ContentType = "application/pdf",
                    FileSize = bytes.LongLength,
                });
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private string[] PriceListLines(Supplier supplier)
    {
        var spec = Suppliers.First(s => s.Name == supplier.Name);
        var names = Products.Where(p => spec.Categories.Contains(p.Category)).Take(12).ToList();
        if (names.Count == 0)
            return new[] { "Services and consumables billed monthly according to the signed quote.", "Demo document." };
        return names.Select(p => $"{p.Name} — {CostFor(p.Price).ToString("0.00", CultureInfo.InvariantCulture)} € / unit (VAT excl.)")
            .Append("Prices valid until 31/12/2026. Demo document.").ToArray();
    }

    private static decimal CostFor(decimal price) => Math.Round(price * 0.32m, 2);

    // ------------------------------------------------------------------ purchases & stock

    private async Task SeedPurchasesAndStockAsync(CancellationToken ct)
    {
        if (await _db.Purchases.AnyAsync(p => p.Reference != null && p.Reference.StartsWith(PurchasePrefix), ct)) return;

        var suppliers = await _db.Suppliers.ToListAsync(ct);
        var products = await _db.Products.Include(p => p.Category).ToListAsync(ct);
        var plans = Suppliers
            .Where(s => s.Categories.Length > 0)
            .Select(s => (Supplier: suppliers.FirstOrDefault(x => x.Name == s.Name), Spec: s))
            .Where(x => x.Supplier is not null)
            .Select(x => (x.Supplier!, x.Spec, Items: products.Where(p => x.Spec.Categories.Contains(p.Category.Name)).ToList()))
            .Where(x => x.Items.Count > 0)
            .ToList();
        if (plans.Count == 0) return;

        _log.LogInformation("Demo content: adding purchases and stock movements.");
        var purchased = new Dictionary<int, decimal>();
        var refNo = 1;
        _db.SkipAuditStamp = true;
        foreach (var (supplier, spec, items) in plans)
        {
            // Active suppliers deliver roughly weekly; the retired one only in the first month.
            var every = spec.Active ? _rng.Next(5, 9) : 10;
            var firstDay = HistoryDays;
            var lastDay = spec.Active ? 1 : HistoryDays - 30;
            for (var day = firstDay - _rng.Next(0, every); day >= lastDay; day -= every)
            {
                var date = DateTime.UtcNow.Date.AddDays(-day).AddHours(9 + _rng.Next(0, 3));
                var purchase = new Purchase
                {
                    SupplierId = supplier.Id,
                    Date = date,
                    Reference = $"{PurchasePrefix}{refNo++:D5}",
                    Notes = _rng.Next(6) == 0 ? "Partial delivery — rest pending." : null,
                    CreatedAt = date,
                };
                foreach (var p in items.OrderBy(_ => _rng.Next()).Take(Math.Min(items.Count, _rng.Next(3, 9))))
                {
                    var qty = (decimal)(_rng.Next(1, 5) * (p.Category.Kind == CategoryKind.Drink ? 12 : 6));
                    purchase.Lines.Add(new PurchaseLine { ProductId = p.Id, Quantity = qty, UnitCost = CostFor(p.Price), CreatedAt = date });
                    _db.StockMovements.Add(new StockMovement
                    {
                        ProductId = p.Id, QuantityChange = qty, Reason = StockMovementReason.Purchase,
                        Note = $"Purchase {purchase.Reference} — {supplier.Name}", CreatedAt = date,
                    });
                    purchased[p.Id] = purchased.GetValueOrDefault(p.Id) + qty;
                }
                _db.Purchases.Add(purchase);
            }
        }
        await _db.SaveChangesAsync(ct);

        // Weekly sale movements from the generated history, then a stock count that tops negatives
        // back up — leaves a believable mix of healthy, low and out-of-stock products.
        var since = DateTime.UtcNow.Date.AddDays(-HistoryDays);
        var ids = purchased.Keys.ToList();
        var sold = await _db.OrderItems
            .Where(i => ids.Contains(i.ProductId) && i.Order.Status == OrderStatus.Paid && i.CreatedAt >= since)
            .Select(i => new { i.ProductId, i.Quantity, i.CreatedAt })
            .ToListAsync(ct);
        var weekly = sold.GroupBy(s => (s.ProductId, Week: (int)((s.CreatedAt - since).TotalDays / 7)));
        var soldTotals = new Dictionary<int, decimal>();
        foreach (var g in weekly)
        {
            var qty = g.Sum(x => x.Quantity);
            _db.StockMovements.Add(new StockMovement
            {
                ProductId = g.Key.ProductId, QuantityChange = -qty, Reason = StockMovementReason.Sale,
                Note = "Weekly sales",
                CreatedAt = Min(since.AddDays(g.Key.Week * 7 + 6).AddHours(23), DateTime.UtcNow.Date.AddHours(-1)),
            });
            soldTotals[g.Key.ProductId] = soldTotals.GetValueOrDefault(g.Key.ProductId) + qty;
        }

        var countDate = DateTime.UtcNow.Date.AddDays(-1).AddHours(8);
        foreach (var product in products.Where(p => purchased.ContainsKey(p.Id)))
        {
            var stock = product.StockQuantity + purchased[product.Id] - soldTotals.GetValueOrDefault(product.Id);
            if (stock < 0)
            {
                var target = _rng.Next(6) == 0 ? 0 : _rng.Next(2, 30);
                _db.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id, QuantityChange = target - stock, Reason = StockMovementReason.Adjustment,
                    Note = "Monthly stock count", CreatedAt = countDate,
                });
                stock = target;
            }
            product.StockQuantity = stock;
        }
        await _db.SaveChangesAsync(ct);
        _db.SkipAuditStamp = false;
    }

    // ------------------------------------------------------------------ albarán scans

    private async Task SeedAlbaranScansAsync(string webRootPath, CancellationToken ct)
    {
        if (await _db.AlbaranScans.AnyAsync(ct)) return;
        var purchases = await _db.Purchases.Include(p => p.Supplier).Include(p => p.Lines).ThenInclude(l => l.Product)
            .Where(p => p.Reference != null && p.Reference.StartsWith(PurchasePrefix))
            .OrderByDescending(p => p.Date).Take(9).ToListAsync(ct);
        if (purchases.Count == 0) return;

        _log.LogInformation("Demo content: adding albarán scans.");
        var dir = Path.Combine(webRootPath, "uploads", "albaranes");
        Directory.CreateDirectory(dir);
        _db.SkipAuditStamp = true;

        async Task<string> WriteImage(string file, string supplier, string number, DateTime date, IEnumerable<(string Desc, decimal Qty, decimal Price)> lines)
        {
            await File.WriteAllTextAsync(Path.Combine(dir, file), AlbaranSvg(supplier, number, date, lines.ToList()), ct);
            return $"/uploads/albaranes/{file}";
        }

        for (var i = 0; i < purchases.Count; i++)
        {
            var p = purchases[i];
            var lines = p.Lines.Select(l => (l.Product.Name, l.Quantity, l.UnitCost)).ToList();
            var number = p.Reference!;
            var url = await WriteImage($"demo-{number.ToLowerInvariant()}.svg", p.Supplier.Name, number, p.Date, lines);
            // The newest three are still waiting for someone to review them; the rest were validated
            // into the purchase they belong to.
            var review = i < 3;
            var scan = new AlbaranScan
            {
                ImageUrl = url,
                SupplierId = p.SupplierId,
                SupplierNameRaw = p.Supplier.Name.ToUpperInvariant(),
                AlbaranNumber = number,
                AlbaranDate = p.Date.Date,
                TotalAmount = Math.Round(lines.Sum(l => l.Quantity * l.UnitCost), 2),
                Status = review ? AlbaranScanStatus.NeedsReview : AlbaranScanStatus.Validated,
                PurchaseId = review ? null : p.Id,
                CreatedAt = p.Date.AddHours(1),
            };
            foreach (var l in p.Lines)
                scan.Lines.Add(new AlbaranScanLine
                {
                    Description = l.Product.Name.ToUpperInvariant(),
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitCost,
                    // Review scans leave one line unmatched so the reviewer has something to fix.
                    ProductId = review && l == p.Lines.First() ? null : l.ProductId,
                    CreatedAt = scan.CreatedAt,
                });
            _db.AlbaranScans.Add(scan);
        }

        // One scan the vision model couldn't read.
        var failedUrl = await WriteImage("demo-unreadable.svg", "???", "—", DateTime.UtcNow.Date, Array.Empty<(string, decimal, decimal)>());
        _db.AlbaranScans.Add(new AlbaranScan
        {
            ImageUrl = failedUrl,
            Status = AlbaranScanStatus.Failed,
            ErrorMessage = "The vision model returned a response that could not be parsed as a delivery note.",
            CreatedAt = DateTime.UtcNow.AddHours(-3),
        });

        await _db.SaveChangesAsync(ct);
        _db.SkipAuditStamp = false;
    }

    // ------------------------------------------------------------------ live orders

    private async Task SeedLiveOrdersAsync(CancellationToken ct)
    {
        if (await _db.Orders.AnyAsync(o => o.Number.StartsWith(LiveOrderPrefix), ct)) return;

        var busyTableIds = await _db.Orders
            .Where(o => o.Status != OrderStatus.Paid && o.Status != OrderStatus.Cancelled)
            .Select(o => o.TableId).ToListAsync(ct);
        var free = await _db.Tables
            .Where(t => !t.IsArchived && t.GroupId == null && t.Status == TableStatus.Available && !busyTableIds.Contains(t.Id))
            .OrderBy(t => t.Name).ToListAsync(ct);
        if (free.Count == 0) return;

        _log.LogInformation("Demo content: opening live orders for the kitchen display.");
        var menu = await LoadMenuAsync(ct);
        var waiters = await _db.Users.Where(u => u.Role == UserRole.Waiter).ToListAsync(ct);
        var drinks = menu.Where(m => m.Category.Kind == CategoryKind.Drink && m.Category.Course != CourseType.Dessert).ToList();
        var starters = menu.Where(m => m.Category.Kind == CategoryKind.Food && m.Category.Course == CourseType.Starter).ToList();
        var mains = menu.Where(m => m.Category.Kind == CategoryKind.Food && m.Category.Course == CourseType.Main).ToList();

        // Each scenario: minutes since opened, order status, (starter status, main status).
        (int Minutes, OrderStatus Status, OrderItemStatus Starter, OrderItemStatus Main)[] scenarios =
        {
            (4, OrderStatus.Open, OrderItemStatus.Pending, OrderItemStatus.Pending),
            (9, OrderStatus.Sent, OrderItemStatus.Pending, OrderItemStatus.Pending),
            (14, OrderStatus.InPreparation, OrderItemStatus.Preparing, OrderItemStatus.Pending),
            (22, OrderStatus.InPreparation, OrderItemStatus.Ready, OrderItemStatus.Preparing),
            (31, OrderStatus.Ready, OrderItemStatus.Delivered, OrderItemStatus.Ready),
            (47, OrderStatus.Delivered, OrderItemStatus.Delivered, OrderItemStatus.Delivered),
            (12, OrderStatus.Sent, OrderItemStatus.Pending, OrderItemStatus.Pending),
            (18, OrderStatus.InPreparation, OrderItemStatus.Preparing, OrderItemStatus.Preparing),
        };

        _db.SkipAuditStamp = true;
        var seq = 1;
        foreach (var (minutes, status, starterStatus, mainStatus) in scenarios)
        {
            if (free.Count == 0) break;
            var table = Pick(free);
            var opened = DateTime.UtcNow.AddMinutes(-minutes);
            var order = new Order
            {
                Number = $"{LiveOrderPrefix}{seq:D4}",
                TableId = table.Id,
                WaiterId = Pick(waiters).Id,
                Status = status,
                CreatedAt = opened,
                DrinksServedAt = minutes > 6 ? opened.AddMinutes(5) : null,
                Notes = seq == 3 ? "Customer in a hurry" : null,
            };
            var guests = Math.Min(table.Seats, _rng.Next(2, 5));
            void Add(List<MenuItem> pool, int count, OrderItemStatus itemStatus)
            {
                foreach (var m in pool.OrderBy(_ => _rng.Next()).Take(count))
                {
                    var item = new OrderItem
                    {
                        ProductId = m.Product.Id, Quantity = 1, UnitPrice = m.Product.Price, VatRate = m.Product.VatRate,
                        Status = itemStatus, CreatedAt = opened.AddMinutes(1),
                        Comment = _rng.Next(4) == 0 && m.Category.Comments.Count > 0 ? Pick(m.Category.Comments.ToList()).Text : null,
                    };
                    if (m.Extras.Count > 0 && _rng.Next(3) == 0)
                    {
                        var e = Pick(m.Extras);
                        item.Extras.Add(new OrderItemExtra { Name = e.Name, Price = e.Price, ExtraId = e.Id, CreatedAt = item.CreatedAt });
                    }
                    order.Items.Add(item);
                }
            }
            Add(drinks, guests, minutes > 6 ? OrderItemStatus.Delivered : OrderItemStatus.Pending);
            Add(starters, Math.Max(1, guests / 2), starterStatus);
            Add(mains, guests, mainStatus);
            _db.Orders.Add(order);
            table.Status = TableStatus.Occupied;
            free.Remove(table);
            seq++;
        }
        await _db.SaveChangesAsync(ct);
        _db.SkipAuditStamp = false;
    }

    // ------------------------------------------------------------------ helpers

    private T Pick<T>(IReadOnlyList<T> list) => list[_rng.Next(list.Count)];

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static string Slug(string s)
    {
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
            if (char.IsLetterOrDigit(ch) && ch < 128) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    /// <summary>The original seeder's flat "emoji on a solid rectangle" image — safe to upgrade.</summary>
    private static bool IsPlainPlaceholder(string url)
    {
        if (!url.StartsWith("data:image/svg+xml;base64,")) return false;
        try
        {
            var svg = Encoding.UTF8.GetString(Convert.FromBase64String(url["data:image/svg+xml;base64,".Length..]));
            return !svg.Contains("radialGradient");
        }
        catch (FormatException) { return false; }
    }

    /// <summary>Self-contained product/extra "photo": radial gradient, soft highlights, a drop shadow and a big emoji.</summary>
    private static string FoodImg(string emoji, string color)
    {
        var light = Mix(color, "#ffffff", 0.45);
        var dark = Mix(color, "#000000", 0.35);
        var svg =
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 400 300'>" +
            $"<defs><radialGradient id='g' cx='50%' cy='38%' r='80%'><stop offset='0' stop-color='{light}'/><stop offset='1' stop-color='{dark}'/></radialGradient></defs>" +
            "<rect width='400' height='300' fill='url(#g)'/>" +
            "<circle cx='345' cy='35' r='80' fill='#fff' fill-opacity='.10'/>" +
            "<circle cx='30' cy='280' r='100' fill='#fff' fill-opacity='.07'/>" +
            "<circle cx='200' cy='150' r='105' fill='#fff' fill-opacity='.12'/>" +
            "<ellipse cx='200' cy='238' rx='95' ry='14' fill='#000' fill-opacity='.22'/>" +
            $"<text x='200' y='200' font-size='140' text-anchor='middle'>{emoji}</text></svg>";
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";
    }

    /// <summary>Round badge used for allergen icons.</summary>
    private static string BadgeImg(string emoji, string color)
    {
        var svg =
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 120 120'>" +
            $"<circle cx='60' cy='60' r='56' fill='{Mix(color, "#ffffff", 0.7)}' stroke='{color}' stroke-width='6'/>" +
            $"<text x='60' y='80' font-size='58' text-anchor='middle'>{emoji}</text></svg>";
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";
    }

    /// <summary>A delivery-note "scan": a slightly rotated sheet of paper with header, lines and total.</summary>
    private static string AlbaranSvg(string supplier, string number, DateTime date, List<(string Desc, decimal Qty, decimal Price)> lines)
    {
        static string E(string s) => SecurityElement.Escape(s) ?? "";
        static string M(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 600 800'>");
        sb.Append("<rect width='600' height='800' fill='#57534e'/>");
        sb.Append("<g transform='rotate(-1.5 300 400)'>");
        sb.Append("<rect x='40' y='30' width='520' height='740' fill='#fdfcf8' stroke='#d6d3d1'/>");
        sb.Append("<g font-family='Courier New, monospace' fill='#1c1917'>");
        sb.Append($"<text x='70' y='85' font-size='22' font-weight='bold'>{E(supplier)}</text>");
        sb.Append("<text x='70' y='112' font-size='14'>DELIVERY NOTE / ALBARÁN</text>");
        sb.Append($"<text x='70' y='140' font-size='14'>No. {E(number)}</text>");
        sb.Append($"<text x='360' y='140' font-size='14'>Date {date:dd/MM/yyyy}</text>");
        sb.Append("<text x='70' y='165' font-size='13'>Customer: LA TOSCANA PIZZERIA</text>");
        sb.Append("<line x1='70' y1='185' x2='530' y2='185' stroke='#1c1917'/>");
        sb.Append("<text x='70' y='205' font-size='12' font-weight='bold'>DESCRIPTION</text>");
        sb.Append("<text x='340' y='205' font-size='12' font-weight='bold'>QTY</text>");
        sb.Append("<text x='400' y='205' font-size='12' font-weight='bold'>PRICE</text>");
        sb.Append("<text x='470' y='205' font-size='12' font-weight='bold'>TOTAL</text>");
        var y = 232;
        foreach (var (desc, qty, price) in lines.Take(18))
        {
            var d = desc.Length > 28 ? desc[..28] : desc;
            sb.Append($"<text x='70' y='{y}' font-size='12'>{E(d.ToUpperInvariant())}</text>");
            sb.Append($"<text x='340' y='{y}' font-size='12'>{M(qty)}</text>");
            sb.Append($"<text x='400' y='{y}' font-size='12'>{M(price)}</text>");
            sb.Append($"<text x='470' y='{y}' font-size='12'>{M(qty * price)}</text>");
            y += 26;
        }
        if (lines.Count == 0)
            sb.Append("<text x='120' y='400' font-size='40' fill='#a8a29e' transform='rotate(-20 300 400)'>BLURRY / UNREADABLE</text>");
        var total = lines.Sum(l => l.Qty * l.Price);
        sb.Append($"<line x1='70' y1='{y}' x2='530' y2='{y}' stroke='#1c1917'/>");
        sb.Append($"<text x='340' y='{y + 30}' font-size='16' font-weight='bold'>TOTAL {M(total)} €</text>");
        sb.Append("<text x='70' y='730' font-size='11'>Received by: ____________   Signature:</text>");
        sb.Append("<path d='M380 725 q20 -25 40 0 t40 0 t30 -10' stroke='#1d4ed8' fill='none' stroke-width='2'/>");
        sb.Append("</g></g></svg>");
        return sb.ToString();
    }

    /// <summary>Returns a lighter or darker variation of a hex colour, cycling through a few steps.</summary>
    private static string Shade(string hex, int index)
    {
        var steps = new[] { 0.0, 0.18, -0.15, 0.32, -0.28, 0.1, -0.08 };
        var step = steps[index % steps.Length];
        return step >= 0 ? Mix(hex, "#ffffff", step) : Mix(hex, "#000000", -step);
    }

    private static string Mix(string a, string b, double t)
    {
        static (int R, int G, int B) Parse(string h)
        {
            h = h.TrimStart('#');
            if (h.Length != 6) return (99, 102, 241);
            return (Convert.ToInt32(h[..2], 16), Convert.ToInt32(h[2..4], 16), Convert.ToInt32(h[4..6], 16));
        }
        var (r1, g1, b1) = Parse(a);
        var (r2, g2, b2) = Parse(b);
        int L(int x, int y) => (int)Math.Round(x + (y - x) * t);
        return $"#{L(r1, r2):x2}{L(g1, g2):x2}{L(b1, b2):x2}";
    }
}
