using Moq;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;
using TicketingService.Core.Services;
using Xunit;

namespace TicketingService.Tests;

/// D3-3 — the root-cause gate on the shared transition service: a manual resolve must record a root
/// cause, while automated/forced sources (ops callback, follow-up auto-close) stay exempt.
public class TicketTransitionServiceTests
{
    private static TicketTransitionService BuildSut()
    {
        var repo = new Mock<ITicketRepository>();
        repo.Setup(r => r.UpdateAsync(It.IsAny<Ticket>())).ReturnsAsync((Ticket t) => t);
        var history = new Mock<ITicketHistoryService>();
        return new TicketTransitionService(repo.Object, history.Object);
    }

    private static Ticket InProgressTicket(string? rootCause = null) => new()
    {
        Id = "t1",
        Status = TicketStatus.InProgress,
        RootCause = rootCause,
    };

    [Fact]
    public async Task Manual_resolve_without_root_cause_is_blocked()
    {
        var sut = BuildSut();
        var ticket = InProgressTicket(rootCause: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.TransitionAsync(ticket, TicketStatus.Resolved, TransitionSource.Manual, "u1"));

        Assert.Equal(TicketStatus.InProgress, ticket.Status);   // unchanged
    }

    [Fact]
    public async Task Manual_resolve_with_root_cause_succeeds()
    {
        var sut = BuildSut();
        var ticket = InProgressTicket(rootCause: "Faulty sensor firmware");

        await sut.TransitionAsync(ticket, TicketStatus.Resolved, TransitionSource.Manual, "u1");

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.NotNull(ticket.ResolvedAt);
    }

    [Fact]
    public async Task System_resolve_without_root_cause_is_exempt()
    {
        var sut = BuildSut();
        var ticket = InProgressTicket(rootCause: null);

        // e.g. the ops work-update callback resolving a ticket — no manual root-cause requirement.
        await sut.TransitionAsync(ticket, TicketStatus.Resolved, TransitionSource.System, "system");

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
    }
}
