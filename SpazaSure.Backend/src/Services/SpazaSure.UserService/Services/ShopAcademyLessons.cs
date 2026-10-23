namespace SpazaSure.UserService.Services;

public record LessonSection(string Heading, string Body);
public record LessonQuestion(string Question, string[] Options, int Answer);
public record Lesson(
    string Key, string Title, string Category, int Minutes, string Summary,
    LessonSection[] Sections, string[] DoThisWeek, LessonQuestion[] Quiz);

/// <summary>
/// The Shop Academy lessons: short, practical lessons on running a spaza as a proper business.
/// Kept in code so every shop gets the same content and it ships with the app's backend.
/// </summary>
public static class ShopAcademyLessons
{
    public static readonly Lesson[] All =
    [
        new("run-as-business", "Run your shop as a real business", "Business basics", 6,
            "Your shop is a business, not your wallet. Separating the two is the first step to growing it.",
            [
                new("Shop money is not home money",
                    "When you take bread for home or pay school fees from the till, the shop loses money you can't see. " +
                    "Keep shop money in its own place (a separate bank account or at least a separate cash box) and only take money out on purpose."),
                new("Pay yourself a salary",
                    "Decide on a fixed amount you pay yourself every week or month. Take only that. What stays behind is the shop's money for new stock, " +
                    "rent and growth. If you take groceries for home, write them down as 'own use' in your stock book."),
                new("Know your numbers",
                    "A business owner knows three numbers: how much came in this month, how much went out, and what stock is on the shelves. " +
                    "Shop Academy keeps these for you if you record your takings and expenses each day."),
            ],
            [
                "Open a separate cash box or bank account for the shop.",
                "Choose a weekly salary for yourself and write it down.",
                "Record today's takings in the Cash book.",
            ],
            [
                new("You need bread for home. What is the right way to take it?",
                    ["Just take it, it is my shop", "Record it as 'own use' in the stock book", "Take money from the till to buy it"], 1),
                new("Why pay yourself a fixed salary?",
                    ["So the shop's money stays for stock, rent and growth", "Because SARS says so", "So you can take more when sales are good"], 0),
            ]),

        new("cash-book", "Keep a daily cash book", "Records", 5,
            "Writing down every rand in and out takes two minutes a day and is the record funders ask for first.",
            [
                new("Count the till every day",
                    "At closing, count the cash and card slips. Record the total as today's takings. Doing it at the same time every day makes it a habit."),
                new("Record every expense",
                    "Stock you buy, transport, electricity, airtime for the card machine, wages, rent: write each one down with the date. " +
                    "Keep slips in an envelope for each month."),
                new("Look at the month",
                    "At month end, compare money in with money out. If more went out than came in, look at which expenses grew. " +
                    "Three to six months of records show a funder or bank that your shop makes money."),
            ],
            [
                "Record today's takings and every expense in the Cash book.",
                "Get an envelope for this month's slips.",
            ],
            [
                new("When is the best time to record takings?",
                    ["Once a month", "At closing, every day", "Only when sales are good"], 1),
                new("How many months of records do funders usually want to see?",
                    ["At least 3 to 6 months", "One week", "None, they just trust you"], 0),
            ]),

        new("stock-control", "Know your stock", "Stock", 6,
            "Stock is cash sitting on your shelves. Counting it tells you what sells, what's running out and what's going missing.",
            [
                new("Count regularly",
                    "Count your fast-moving items (bread, milk, airtime, cigarettes, cool drinks) every week and everything else every month. " +
                    "Record the count in the stock book. The difference between what you expected and what you counted shows losses."),
                new("Set reorder levels",
                    "For each item decide the lowest number you want on the shelf before you buy more. Shop Academy warns you when items reach it, " +
                    "so you don't run out of what customers come for."),
                new("First in, first out",
                    "Put new stock behind old stock so the oldest sells first. This stops products expiring on the shelf."),
            ],
            [
                "Add your 10 best-selling items to the stock book with reorder levels.",
                "Do a stock count of those items this week.",
            ],
            [
                new("You expected 24 loaves but counted 20. What does the difference show?",
                    ["Nothing, it's normal", "Stock that was sold, taken or lost without being recorded", "A supplier mistake"], 1),
                new("What does 'first in, first out' mean?",
                    ["Sell the oldest stock first", "Serve the first customer first", "Pay the first supplier first"], 0),
            ]),

        new("pricing-margins", "Price for profit", "Money", 7,
            "Selling a lot doesn't help if the price doesn't cover your costs. Learn markup and margin.",
            [
                new("Markup",
                    "Markup is what you add on top of cost. If a cool drink costs you R10 and you sell it for R13, your markup is R3, or 30%."),
                new("Cover all your costs",
                    "Your markup must pay for rent, electricity, transport, wages and your salary, not only the stock. " +
                    "Most spaza items need 20% to 40% markup; fast sellers like bread can have less, slow sellers and convenience items more."),
                new("Check your prices",
                    "When a supplier's price goes up, update your selling price. Shop Academy shows each item's markup so you can see which items earn little."),
            ],
            [
                "Check the markup on your 10 best sellers in the stock book.",
                "Update any item where the markup is below 15%.",
            ],
            [
                new("An item costs R20 and you sell it for R25. What is the markup?",
                    ["5%", "25%", "20%"], 1),
                new("What must your markup pay for?",
                    ["Only the stock", "Stock plus rent, electricity, transport, wages and your salary", "Nothing, volume is enough"], 1),
            ]),

        new("reduce-losses", "Stop losing stock and money", "Stock", 6,
            "Expired goods, theft and unpaid credit quietly eat your profit. Each one can be controlled.",
            [
                new("Expiry and spoilage",
                    "Check dates every week. Move items that expire soon to the front or sell them at a small discount before they expire. " +
                    "Record anything thrown away as 'spoiled' so you know what it cost you."),
                new("Theft",
                    "Keep high-value items (cigarettes, airtime, alcohol where licensed) behind the counter. Count them often. " +
                    "Record missing stock as 'stolen' so the pattern is visible."),
                new("Credit (the book)",
                    "Giving goods on credit is cash you don't have. Set a limit per customer, write every amount down and collect on payday."),
            ],
            [
                "Check expiry dates on your shelves today.",
                "Record any spoiled or missing items in the stock book.",
            ],
            [
                new("A box of yoghurt expired. What should you do in the stock book?",
                    ["Nothing", "Record it as 'spoiled'", "Delete the item"], 1),
                new("Where should high-value items like airtime be kept?",
                    ["On the open shelf", "Behind the counter", "In the storeroom with the door open"], 1),
            ]),

        new("buying-smart", "Buy smart and manage cash flow", "Money", 6,
            "How and when you buy decides whether you have cash when you need it.",
            [
                new("Buy what sells",
                    "Use your stock book to see what sells fastest. Put most of your money into those items and buy slow sellers in small quantities."),
                new("Buy together, pay less",
                    "SpazaSure group buys let shops in your area buy together for a bigger discount. Join one when it covers items you sell anyway."),
                new("Keep a cash cushion",
                    "Try to keep enough money aside to cover one week of stock and your fixed bills. This keeps the shelves full when sales are slow."),
            ],
            [
                "List your 5 fastest sellers from the stock book.",
                "Check SpazaSure group buys for those items.",
            ],
            [
                new("Where should most of your stock money go?",
                    ["Slow-selling luxury items", "Your fastest-selling items", "Whatever is on special"], 1),
                new("What is a cash cushion?",
                    ["Money set aside for a week of stock and fixed bills", "A loan from a friend", "Cash kept under the counter for change"], 0),
            ]),

        new("food-safety", "Food safety and compliance", "Compliance", 5,
            "Safe food protects your customers and keeps your shop open. Inspectors and customers notice.",
            [
                new("Store food correctly",
                    "Keep fridges at 5°C or colder. Keep food off the floor and away from chemicals like paraffin and cleaning products. " +
                    "Keep the shop clean and free of pests."),
                new("Only sell genuine products",
                    "Buy from trusted suppliers who give invoices. Fake and expired products can make people sick. " +
                    "SpazaSure products are traceable, and customers can verify them with the app."),
                new("Keep your papers up to date",
                    "Your trading permit, certificate of acceptability (food premises) and health certificates must be current. " +
                    "Upload them in the Documents section so your SpazaSure status stays green."),
            ],
            [
                "Check your fridge temperature.",
                "Make sure your documents in SpazaSure are up to date.",
            ],
            [
                new("What temperature should a fridge be?",
                    ["5°C or colder", "15°C", "Room temperature"], 0),
                new("Where should paraffin and cleaning products be kept?",
                    ["Next to the bread", "Away from food", "On the counter"], 1),
            ]),

        new("formalise", "Register your business", "Business basics", 7,
            "A registered business can open business bank accounts, sign contracts and apply for funding.",
            [
                new("CIPC registration",
                    "Registering a company with CIPC (www.bizportal.gov.za) costs little and takes a few days. " +
                    "Many owners start as a sole proprietor and register a (Pty) Ltd when they grow."),
                new("Tax with SARS",
                    "Register for income tax with SARS. Small businesses with a turnover under R1 million a year can use turnover tax, which is simpler. " +
                    "You only need to register for VAT if you sell more than R1 million a year."),
                new("Business bank account and permits",
                    "Open a business bank account so shop money is separate and your bank statements become proof of trading. " +
                    "Get a trading permit from your municipality."),
            ],
            [
                "Fill in your Business details in Shop Academy.",
                "Find out what you need to register on bizportal.gov.za.",
            ],
            [
                new("Where do you register a company in South Africa?",
                    ["CIPC (bizportal.gov.za)", "The police station", "Your supplier"], 0),
                new("When must a business register for VAT?",
                    ["Always", "When sales go over R1 million a year", "Never"], 1),
            ]),

        new("funding-ready", "Get ready for funding", "Growth", 7,
            "Funders want to see that your shop is real, makes money and is well run. Your Shop Academy records are that proof.",
            [
                new("What funders look at",
                    "Lenders and programmes such as SEFA, NYDA, provincial development agencies and banks ask for: proof the business is registered, " +
                    "an ID, bank statements, records of sales and expenses for at least 3 to 6 months, and what the money will be used for."),
                new("Use your Business Record",
                    "Shop Academy builds a Business Record PDF from your cash book, stock book, SpazaSure purchase history and verification status. " +
                    "The SpazaSure purchases and verification are confirmed by SpazaSure; the rest comes from your own records, so keep them honest and complete."),
                new("Have a plan",
                    "Know exactly what you need: e.g. a new fridge (R12,000) that lets you sell cold drinks and meat, adding R4,000 sales a month. " +
                    "A clear plan with numbers is much easier to fund."),
            ],
            [
                "Look at your Funding readiness checklist in Shop Academy.",
                "Write down one thing you would use funding for and what it costs.",
            ],
            [
                new("What do funders usually ask for?",
                    ["Only your ID", "Registration, ID, bank statements and 3 to 6 months of records", "A letter from a friend"], 1),
                new("Which part of the Business Record does SpazaSure confirm?",
                    ["Your cash book takings", "Your SpazaSure purchases and verification status", "Your goals"], 1),
            ]),

        new("customer-loyalty", "Keep customers coming back", "Growth", 5,
            "Loyal customers are steady income. Good service and trust keep them buying from you.",
            [
                new("Service and trust",
                    "Greet customers, keep the shop clean and price items clearly. Customers come back to shops they trust. " +
                    "Your SpazaSure sticker shows customers you are verified."),
                new("Have what they need",
                    "Ask customers what they look for and can't find. Running out of everyday items sends customers to the next shop."),
                new("Listen to reviews",
                    "Customers can review your shop in the SpazaSure app. Thank people for good reviews and reply politely to complaints."),
            ],
            [
                "Ask three customers what they would like you to stock.",
                "Reply to your SpazaSure reviews.",
            ],
            [
                new("A customer leaves a complaint review. What should you do?",
                    ["Ignore it", "Reply politely and fix the problem", "Argue in the reply"], 1),
                new("Why does running out of everyday items hurt?",
                    ["It doesn't", "Customers go to another shop and may not come back", "Suppliers charge more"], 1),
            ]),
    ];

    public static Lesson? Find(string key) => All.FirstOrDefault(l => l.Key == key);

    /// <summary>A lesson counts as passed when at least two thirds of its questions are right.</summary>
    public static bool Passed(int score, int total) => total == 0 || score * 3 >= total * 2;
}
