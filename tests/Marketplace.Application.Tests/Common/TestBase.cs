using Marketplace.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace Marketplace.Application.Tests.Common;

public abstract class TestBase : IDisposable
{
    protected static readonly DateTime FixedTime = new(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);

    protected TestAppDbContext DbContext { get; }
    protected IClock Clock { get; }
    protected IUnitOfWork UnitOfWork { get; }

    protected TestBase()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        DbContext = new TestAppDbContext(options);

        Clock = Substitute.For<IClock>();
        Clock.UtcNow.Returns(FixedTime);

        UnitOfWork = Substitute.For<IUnitOfWork>();
        UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(call => DbContext.SaveChangesAsync(call.Arg<CancellationToken>()));
    }

    public void Dispose()
    {
        DbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
