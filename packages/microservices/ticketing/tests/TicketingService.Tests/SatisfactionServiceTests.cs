using AutoMapper;
using Microsoft.AspNetCore.Http;
using Moq;
using TicketingService.Core.DTOs.Satisfaction;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;
using TicketingService.Core.Services;
using Xunit;

namespace TicketingService.Tests;

/// D6-2 — a satisfaction rating below 3 raises a low-satisfaction alert; 3+ does not.
public class SatisfactionServiceTests
{
    private readonly Mock<IAlertService> _alerts = new();

    private SatisfactionService BuildSut()
    {
        var ratings = new Mock<ISatisfactionRatingRepository>();
        ratings.Setup(r => r.GetByTicketIdAsync(It.IsAny<string>())).ReturnsAsync((TicketSatisfactionRating?)null);
        ratings.Setup(r => r.CreateAsync(It.IsAny<TicketSatisfactionRating>()))
               .ReturnsAsync((TicketSatisfactionRating r) => r);

        var tickets = new Mock<ITicketRepository>();
        tickets.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<bool>()))
               .ReturnsAsync(new Ticket { Id = "t1", TicketNumber = 7, Status = TicketStatus.Resolved, Title = "Broken scale" });

        var categories = new Mock<ITicketCategoryRepository>();
        var sla = new Mock<ISLAService>();
        var http = new Mock<IHttpContextAccessor>();
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<SatisfactionRatingReadDto>(It.IsAny<object>())).Returns(new SatisfactionRatingReadDto());

        return new SatisfactionService(ratings.Object, tickets.Object, categories.Object,
            sla.Object, _alerts.Object, http.Object, mapper.Object);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(5, false)]
    public async Task Low_scores_alert_higher_scores_do_not(int rating, bool shouldAlert)
    {
        var sut = BuildSut();

        await sut.SubmitRatingAsync("t1", new SubmitRatingDto { Rating = rating }, "u1");

        _alerts.Verify(a => a.CreateAsync(
            It.IsAny<string>(), "LowSatisfaction", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            shouldAlert ? Times.Once() : Times.Never());
    }
}
