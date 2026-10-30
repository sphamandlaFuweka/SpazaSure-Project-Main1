using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.Infrastructure.Data;

/// <summary>Tells shops what happened to their group buy money.</summary>
public static class GroupBuyNotices
{
    public static void Refunds(EventPublisher events, GroupBuy groupBuy, IEnumerable<(GroupBuyParticipant Participant, decimal Refund)> refunds)
    {
        foreach (var (participant, refund) in refunds)
            events.PublishNotification(participant.Shop.UserId.ToString(), "wallet", "Group buy refund",
                $"'{groupBuy.Title}' {(groupBuy.Status == "cancelled" ? "was cancelled" : "didn't reach its target")}, so R{refund:N2} is back in your SpazaSure wallet.",
                referenceId: groupBuy.Id.ToString(), routingKey: "notification.group_buy");
    }

    public static void CardPayment(EventPublisher events, GroupBuy groupBuy, GroupBuyParticipant participant, GroupBuyPayments.CardResult result)
    {
        if (result == GroupBuyPayments.CardResult.Joined)
            events.PublishNotification(participant.Shop.UserId.ToString(), "group_buy", "You joined the group buy",
                $"We received R{participant.AmountPaid:N2} for '{groupBuy.Title}'. Your order is placed when the target is reached.",
                referenceId: groupBuy.Id.ToString(), routingKey: "notification.group_buy");
        else if (result == GroupBuyPayments.CardResult.RefundedToWallet)
            events.PublishNotification(participant.Shop.UserId.ToString(), "wallet", "Group buy payment added to your wallet",
                $"'{groupBuy.Title}' filled up or closed before your payment came through, so R{participant.AmountPaid:N2} is in your SpazaSure wallet.",
                referenceId: groupBuy.Id.ToString(), routingKey: "notification.group_buy");
    }

    /// <summary>The shops in the campaign: joined (paid) or already ordered.</summary>
    private static IEnumerable<string> Members(GroupBuy groupBuy) =>
        groupBuy.Participants.Where(p => p.Status is "joined" or "confirmed").Select(p => p.Shop.UserId.ToString());

    public static void Extended(EventPublisher events, GroupBuy groupBuy) =>
        events.PublishNotificationToMany(Members(groupBuy), "group_buy", "Group buy extended by a week",
            $"'{groupBuy.Title}' hasn't reached its target yet, so it now runs until {groupBuy.ExpiresAt:d MMM}. Share it with nearby shops to fill it.",
            referenceId: groupBuy.Id.ToString(), routingKey: "notification.group_buy");

    public static void EndingSoon(EventPublisher events, GroupBuy groupBuy, int itemsStillNeeded)
    {
        var after = groupBuy.ExtensionCount < GroupBuyPayments.MaxExtensions
            ? "If the target isn't reached, it gets one more week."
            : "If the target isn't reached, your money goes back to your wallet.";
        var needed = itemsStillNeeded > 0
            ? $"{itemsStillNeeded} more items are needed. Share it with nearby shops. {after}"
            : "Its targets are reached; the supplier will accept soon.";
        events.PublishNotificationToMany(Members(groupBuy), "group_buy", "Group buy ends in 2 days",
            $"'{groupBuy.Title}' ends on {groupBuy.ExpiresAt:d MMM}. {needed}",
            priority: "high", referenceId: groupBuy.Id.ToString(), routingKey: "notification.group_buy");
    }
}
