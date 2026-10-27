using System.Net;
using System.Text;

namespace SpazaSure.NotificationService.Email;

/// <summary>A ready-to-send email.</summary>
public sealed record RenderedEmail(string Subject, string Html, string Text);

/// <summary>Everything a template needs about the reader and where links go.</summary>
public sealed record EmailContext(
    string Role, string? Name, string AppUrl, string PortalUrl, string? UnsubscribeUrl);

/// <summary>
/// The words of every automated email, per role. Kept short and practical: one idea per email,
/// one button, and a standing invitation to reply while SpazaSure is being tested.
/// </summary>
public static class EmailTemplates
{
    public static readonly string[] Steps =
    [
        EmailSequencePlanner.Welcome, "education_1", "education_2", "education_3",
        EmailSequencePlanner.Inactive7, EmailSequencePlanner.Inactive21,
    ];

    public static readonly string[] Roles = ["customer", "spaza_owner", "supplier"];

    private sealed record Content(string Subject, string Heading, string[] Paragraphs, string[] Points, string Button);

    public static RenderedEmail Render(string step, EmailContext c)
    {
        var content = For(step, c.Role) ?? throw new ArgumentException($"Unknown email '{step}' for role '{c.Role}'.");
        var buttonUrl = c.Role == "supplier" ? c.PortalUrl : c.AppUrl;
        return Build(content, c.Name, buttonUrl, c.UnsubscribeUrl,
            footer: "You're getting this because you have a SpazaSure account.");
    }

    public static RenderedEmail WaitlistConfirmation(string? name, string confirmUrl, string unsubscribeUrl) =>
        Build(new Content(
                "Confirm your spot on the SpazaSure waitlist",
                "One click to confirm",
                ["Thanks for joining the SpazaSure waitlist. Please confirm your email address so we can let you know when it's your turn."],
                [],
                "Confirm my email"),
            name, confirmUrl, unsubscribeUrl,
            footer: "You're getting this because this address was entered on the SpazaSure waitlist. If that wasn't you, ignore this email and you won't hear from us again.");

    private static Content? For(string step, string role) => (step, role) switch
    {
        // ── Shoppers ────────────────────────────────────────────────────────────
        ("welcome", "customer") => new(
            "Welcome to SpazaSure",
            "Check it before you buy it",
            ["SpazaSure helps you make sure what you buy at your local spaza shop is real, safe and not expired."],
            ["Scan any barcode with your phone camera.",
             "See photos of the real product and compare them with the pack in your hand.",
             "Report anything that looks fake, so others are warned too."],
            "Scan my first product"),
        ("education_1", "customer") => new(
            "Is it the real thing? Find out in 10 seconds",
            "Compare it with the real product",
            ["After you scan a product, tap \"Compare with my pack\" and take a photo of it. SpazaSure puts it next to photos of the real product and checks the brand, name, size and barcode printed on your pack."],
            [],
            "Try it now"),
        ("education_2", "customer") => new(
            "Earn points every time you scan",
            "Your scans earn rewards",
            ["Every product you check earns you reward points, once per product each day. Keep an eye on your points under Rewards in the app."],
            [],
            "See my points"),
        ("education_3", "customer") => new(
            "Is your spaza shop verified?",
            "Look for the SpazaSure sticker",
            ["Verified spaza shops show a SpazaSure sticker. Scan it with the app to see that the shop is registered and checked. If something seems wrong, you can report it in the app."],
            [],
            "Check a shop"),

        // ── Spaza shops ─────────────────────────────────────────────────────────
        ("welcome", "spaza_owner") => new(
            "Welcome to SpazaSure",
            "Stock your shop with confidence",
            ["SpazaSure connects your shop with verified suppliers, so the stock on your shelves is genuine."],
            ["Order stock from verified suppliers and follow your delivery.",
             "Join group buys with other shops to pay less.",
             "Keep a simple stock and cash book in Shop Academy."],
            "Open SpazaSure"),
        ("education_1", "spaza_owner") => new(
            "Order stock from verified suppliers",
            "Your first order",
            ["Browse the marketplace, add what you need to your cart and check out. You can follow your order all the way to your door, and rate the delivery when it arrives."],
            [],
            "Browse the marketplace"),
        ("education_2", "spaza_owner") => new(
            "Buy together, pay less",
            "Group buys",
            ["In a group buy, shops order the same product together to reach a lower price. Join one that's open, or share it with shops you know."],
            [],
            "See group buys"),
        ("education_3", "spaza_owner") => new(
            "Know what you have and what you earn",
            "Shop Academy",
            ["Shop Academy keeps your stock book and cash book in one place. Your SpazaSure orders can be added to your stock in one tap, so you always know what's on the shelf and what you made this month."],
            [],
            "Open Shop Academy"),

        // ── Suppliers ───────────────────────────────────────────────────────────
        ("welcome", "supplier") => new(
            "Welcome to SpazaSure",
            "Reach spaza shops directly",
            ["SpazaSure lets spaza shops order from you directly, and lets their customers check that your products are genuine."],
            ["Finish your profile and upload your company documents to get verified.",
             "Add your products with photos and barcodes.",
             "Receive orders and get paid out."],
            "Open the supplier portal"),
        ("education_1", "supplier") => new(
            "Get your verified badge",
            "Verification builds trust",
            ["Shops prefer verified suppliers. Upload your company documents in the portal; once our team has checked them, your products show the verified badge."],
            [],
            "Upload my documents"),
        ("education_2", "supplier") => new(
            "Photos and barcodes protect your brand",
            "Help customers spot fakes",
            ["When a customer scans one of your products, SpazaSure shows them your product photos to compare with the pack in their hand. Clear photos of the front of the pack and the right barcode make fakes easy to spot."],
            [],
            "Update my products"),
        ("education_3", "supplier") => new(
            "How you get paid",
            "Orders and payouts",
            ["When a shop's order is delivered, the money is set aside for you and paid out to your bank account. You can follow every order and payout in the portal."],
            [],
            "See my orders"),

        // ── Reminders (every role) ──────────────────────────────────────────────
        ("inactive_7", _) => new(
            "We haven't seen you in a while",
            "Anything we can help with?",
            [role switch
            {
                "customer" => "Next time you shop, scan a product before you buy it. It takes a few seconds.",
                "spaza_owner" => "Running low on stock? Your verified suppliers are a few taps away.",
                _ => "Shops may be looking for your products. Check your orders and keep your catalogue up to date.",
            },
             "If something didn't work or wasn't clear, just reply to this email. We read every message."],
            [],
            "Open SpazaSure"),
        ("inactive_21", _) => new(
            "Can you help us improve SpazaSure?",
            "One question",
            ["We're still testing SpazaSure, and your experience matters to us. What stopped you from using it more? Reply with one sentence; it helps more than you think."],
            [],
            "Open SpazaSure"),
        _ => null,
    };

    private static RenderedEmail Build(Content c, string? name, string buttonUrl, string? unsubscribeUrl, string footer)
    {
        var greeting = string.IsNullOrWhiteSpace(name) ? "Hi there," : $"Hi {name.Trim()},";
        const string testing = "SpazaSure is still in testing. If anything doesn't work, reply to this email or tap \"Send feedback\" in the app.";

        // Plain text first: some people read email in clients that don't show HTML
        var text = new StringBuilder()
            .AppendLine(greeting).AppendLine();
        foreach (var p in c.Paragraphs) text.AppendLine(p).AppendLine();
        foreach (var point in c.Points) text.AppendLine($"- {point}");
        if (c.Points.Length > 0) text.AppendLine();
        text.AppendLine($"{c.Button}: {buttonUrl}").AppendLine()
            .AppendLine(testing).AppendLine()
            .AppendLine("The SpazaSure team").AppendLine()
            .AppendLine(footer);
        if (unsubscribeUrl is not null) text.AppendLine($"Unsubscribe: {unsubscribeUrl}");

        static string E(string s) => WebUtility.HtmlEncode(s);
        var html = new StringBuilder();
        html.Append("<!doctype html><html><body style=\"margin:0;padding:0;background:#F4F5FA;\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#F4F5FA;padding:24px 12px;\"><tr><td align=\"center\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;background:#FFFFFF;border-radius:16px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;color:#1B1B2F;\">")
            .Append("<tr><td style=\"background:#25449A;padding:20px 28px;color:#FFFFFF;font-size:20px;font-weight:bold;\">SpazaSure</td></tr>")
            .Append("<tr><td style=\"padding:28px;\">")
            .Append($"<h1 style=\"margin:0 0 16px;font-size:22px;line-height:1.3;\">{E(c.Heading)}</h1>")
            .Append($"<p style=\"margin:0 0 14px;font-size:15px;line-height:1.6;\">{E(greeting)}</p>");
        foreach (var p in c.Paragraphs)
            html.Append($"<p style=\"margin:0 0 14px;font-size:15px;line-height:1.6;\">{E(p)}</p>");
        if (c.Points.Length > 0)
        {
            html.Append("<ul style=\"margin:0 0 16px;padding-left:20px;font-size:15px;line-height:1.6;\">");
            foreach (var point in c.Points) html.Append($"<li style=\"margin-bottom:6px;\">{E(point)}</li>");
            html.Append("</ul>");
        }
        html.Append($"<p style=\"margin:22px 0;\"><a href=\"{E(buttonUrl)}\" style=\"display:inline-block;background:#078C50;color:#FFFFFF;text-decoration:none;font-weight:bold;font-size:15px;padding:12px 22px;border-radius:10px;\">{E(c.Button)}</a></p>")
            .Append($"<p style=\"margin:0 0 14px;font-size:13px;line-height:1.6;color:#55556B;background:#F4F5FA;padding:12px;border-radius:10px;\">{E(testing)}</p>")
            .Append("<p style=\"margin:0;font-size:15px;\">The SpazaSure team</p>")
            .Append("</td></tr>")
            .Append($"<tr><td style=\"padding:16px 28px 24px;font-size:12px;line-height:1.5;color:#8888AA;\">{E(footer)}");
        if (unsubscribeUrl is not null)
            html.Append($" <a href=\"{E(unsubscribeUrl)}\" style=\"color:#8888AA;\">Unsubscribe</a>");
        html.Append("</td></tr></table></td></tr></table></body></html>");

        return new RenderedEmail(c.Subject, html.ToString(), text.ToString());
    }
}
