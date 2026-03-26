using CodeMigrationTool.Server.Sessions;

namespace CodeMigrationTool.Server.Tests.Sessions;

public class SessionManagerTests
{
    private readonly SessionManager _sut = new();

    [Fact]
    public void Create_ReturnsSessionWithUniqueId()
    {
        var session = _sut.Create("sandbox-1", 50100);

        Assert.NotNull(session.SessionId);
        Assert.Equal("sandbox-1", session.SandboxId);
        Assert.Equal(50100, session.GrpcPort);
    }

    [Fact]
    public void Create_MultipleSessions_HaveDistinctIds()
    {
        var s1 = _sut.Create("sandbox-1", 50100);
        var s2 = _sut.Create("sandbox-2", 50101);

        Assert.NotEqual(s1.SessionId, s2.SessionId);
    }

    [Fact]
    public void Get_ExistingSession_ReturnsSession()
    {
        var created = _sut.Create("sandbox-1", 50100);
        var retrieved = _sut.Get(created.SessionId);

        Assert.Equal(created.SessionId, retrieved.SessionId);
        Assert.Equal("sandbox-1", retrieved.SandboxId);
    }

    [Fact]
    public void Get_NonExistentSession_ThrowsKeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(() => _sut.Get("nonexistent"));
    }

    [Fact]
    public void Get_UpdatesLastAccessedAt()
    {
        var session = _sut.Create("sandbox-1", 50100);
        var firstAccess = session.LastAccessedAt;

        // Small delay to ensure time difference
        Thread.Sleep(10);
        _sut.Get(session.SessionId);

        Assert.True(session.LastAccessedAt >= firstAccess);
    }

    [Fact]
    public void Remove_ExistingSession_CanNoLongerRetrieve()
    {
        var session = _sut.Create("sandbox-1", 50100);
        _sut.Remove(session.SessionId);

        Assert.Throws<KeyNotFoundException>(() => _sut.Get(session.SessionId));
    }

    [Fact]
    public void Remove_NonExistentSession_DoesNotThrow()
    {
        _sut.Remove("nonexistent"); // Should not throw
    }

    [Fact]
    public void TryGet_ExistingSession_ReturnsTrueAndSession()
    {
        var created = _sut.Create("sandbox-1", 50100);

        var found = _sut.TryGet(created.SessionId, out var session);

        Assert.True(found);
        Assert.NotNull(session);
        Assert.Equal(created.SessionId, session.SessionId);
    }

    [Fact]
    public void TryGet_NonExistentSession_ReturnsFalse()
    {
        var found = _sut.TryGet("nonexistent", out var session);

        Assert.False(found);
    }

    [Fact]
    public void ListActive_ReturnsAllSessions()
    {
        _sut.Create("sandbox-1", 50100);
        _sut.Create("sandbox-2", 50101);

        var active = _sut.ListActive();

        Assert.Equal(2, active.Count);
    }

    [Fact]
    public void GetExpiredSessions_ReturnsOldSessions()
    {
        var session = _sut.Create("sandbox-1", 50100);

        // No sessions should be expired with a generous TTL
        var expired = _sut.GetExpiredSessions(TimeSpan.FromHours(1));
        Assert.Empty(expired);

        // All sessions should be expired with zero TTL
        expired = _sut.GetExpiredSessions(TimeSpan.Zero);
        Assert.Single(expired);
    }
}
