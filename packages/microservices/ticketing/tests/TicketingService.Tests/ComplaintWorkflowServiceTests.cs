using AutoMapper;
using Microsoft.AspNetCore.Http;
using Moq;
using TicketingService.Core.DTOs.Complaints;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;
using TicketingService.Core.Services;
using Xunit;

namespace TicketingService.Tests;

/// D5 — the complaint 5-step workflow: auto-init, strict sequential gating, and the step-specific
/// requirements (satisfaction + sign-off on step 4, root cause + preventive action on step 5).
public class ComplaintWorkflowServiceTests
{
    private readonly List<ComplaintWorkflowStep> _store = new();
    private readonly Ticket _ticket = new() { Id = "t1", TicketNumber = 7, Status = TicketStatus.InProgress };
    private readonly Mock<ITicketTransitionService> _transition = new();

    private ComplaintWorkflowService BuildSut()
    {
        var steps = new Mock<IComplaintWorkflowStepRepository>();
        steps.Setup(r => r.GetByTicketAsync(It.IsAny<string>()))
             .ReturnsAsync(() => _store.OrderBy(s => s.StepNumber).ToList());
        steps.Setup(r => r.CreateAsync(It.IsAny<ComplaintWorkflowStep>()))
             .ReturnsAsync((ComplaintWorkflowStep s) => { _store.Add(s); return s; });
        steps.Setup(r => r.UpdateAsync(It.IsAny<ComplaintWorkflowStep>()))
             .ReturnsAsync((ComplaintWorkflowStep s) => s);   // store holds the same refs

        var tickets = new Mock<ITicketRepository>();
        tickets.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(_ticket);

        var history = new Mock<ITicketHistoryService>();
        var alerts = new Mock<IAlertService>();
        var crm = new Mock<TicketingService.Core.Integrations.ICrmSync>();   // D8 — disabled by default
        // D8-1 — only consulted on closure when the CRM sync is enabled, which it is not here, so a bare
        // mock is the honest stand-in rather than one set up to return a customer nobody asks for.
        var customers = new Mock<ICustomerService>();
        var http = new Mock<IHttpContextAccessor>();
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<IEnumerable<ComplaintStepReadDto>>(It.IsAny<object>()))
              .Returns(Array.Empty<ComplaintStepReadDto>());

        return new ComplaintWorkflowService(steps.Object, tickets.Object, _transition.Object,
            history.Object, alerts.Object, crm.Object, customers.Object, http.Object, mapper.Object);
    }

    [Fact]
    public async Task Initialize_creates_five_steps_with_first_active()
    {
        var sut = BuildSut();
        await sut.InitializeAsync("t1");

        Assert.Equal(5, _store.Count);
        Assert.Equal(ComplaintStepStatus.Active, _store.Single(s => s.StepNumber == 1).Status);
        Assert.All(_store.Where(s => s.StepNumber > 1),
            s => Assert.Equal(ComplaintStepStatus.Pending, s.Status));
    }

    [Fact]
    public async Task Cannot_complete_a_step_out_of_order()
    {
        var sut = BuildSut();
        await sut.InitializeAsync("t1");

        // Step 2 is Pending while step 1 is Active → completing it must fail.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CompleteStepAsync("t1", 2, new CompleteComplaintStepDto(), "u1"));
    }

    [Fact]
    public async Task Completing_a_step_activates_the_next()
    {
        var sut = BuildSut();
        await sut.InitializeAsync("t1");

        await sut.CompleteStepAsync("t1", 1, new CompleteComplaintStepDto { Notes = "ack" }, "u1");

        Assert.Equal(ComplaintStepStatus.Completed, _store.Single(s => s.StepNumber == 1).Status);
        Assert.Equal(ComplaintStepStatus.Active, _store.Single(s => s.StepNumber == 2).Status);
    }

    [Fact]
    public async Task Dissatisfied_satisfaction_check_requires_sign_off()
    {
        var sut = BuildSut();
        await sut.InitializeAsync("t1");
        await sut.CompleteStepAsync("t1", 1, new CompleteComplaintStepDto(), "u1");
        await sut.CompleteStepAsync("t1", 2, new CompleteComplaintStepDto(), "u1");
        await sut.CompleteStepAsync("t1", 3, new CompleteComplaintStepDto(), "u1");

        // Step 4 dissatisfied, no sign-off → blocked.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CompleteStepAsync("t1", 4, new CompleteComplaintStepDto { SatisfactionMet = false }, "u1"));

        // With a sign-off it proceeds.
        await sut.CompleteStepAsync("t1", 4,
            new CompleteComplaintStepDto { SatisfactionMet = false, SignOffByUserId = "md-1" }, "u1");
        Assert.Equal(ComplaintStepStatus.Active, _store.Single(s => s.StepNumber == 5).Status);
    }

    [Fact]
    public async Task Close_step_requires_root_cause_and_preventive_action_then_closes_ticket()
    {
        var sut = BuildSut();
        await sut.InitializeAsync("t1");
        await sut.CompleteStepAsync("t1", 1, new CompleteComplaintStepDto(), "u1");
        await sut.CompleteStepAsync("t1", 2, new CompleteComplaintStepDto(), "u1");
        await sut.CompleteStepAsync("t1", 3, new CompleteComplaintStepDto(), "u1");
        await sut.CompleteStepAsync("t1", 4, new CompleteComplaintStepDto { SatisfactionMet = true }, "u1");

        // Missing preventive action → blocked.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CompleteStepAsync("t1", 5, new CompleteComplaintStepDto { RootCause = "x" }, "u1"));

        await sut.CompleteStepAsync("t1", 5,
            new CompleteComplaintStepDto { RootCause = "Late delivery", PreventiveAction = "Add SLA reminder" }, "u1");

        Assert.Equal(ComplaintStepStatus.Completed, _store.Single(s => s.StepNumber == 5).Status);
        Assert.Equal("Late delivery", _ticket.RootCause);
        // The ticket was driven to Closed via a forced transition.
        _transition.Verify(t => t.TransitionAsync(_ticket, TicketStatus.Closed,
            TransitionSource.System, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Once);
    }
}
