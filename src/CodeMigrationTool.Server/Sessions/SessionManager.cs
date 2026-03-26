using System.Collections.Concurrent;

namespace CodeMigrationTool.Server.Sessions;

/// <summary>
/// Tracks active sessions mapping session IDs to sandbox instances.
/// Thread-safe via ConcurrentDictionary.
/// </summary>
public class SessionManager
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();

    public SessionInfo Create(string sandboxId)
    {
        var session = new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString("N"),
            SandboxId = sandboxId
        };

        if (!_sessions.TryAdd(session.SessionId, session))
            throw new InvalidOperationException("Failed to create session (duplicate ID)");

        return session;
    }

    public SessionInfo Get(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new KeyNotFoundException($"Session not found: {sessionId}");

        session.LastAccessedAt = DateTime.UtcNow;
        return session;
    }

    public bool TryGet(string sessionId, out SessionInfo? session)
    {
        var found = _sessions.TryGetValue(sessionId, out session);
        if (found && session is not null)
            session.LastAccessedAt = DateTime.UtcNow;
        return found;
    }

    public void Remove(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }

    public IReadOnlyCollection<SessionInfo> ListActive()
    {
        return _sessions.Values.ToList();
    }

    /// <summary>
    /// Returns sessions that haven't been accessed within the specified TTL.
    /// </summary>
    public IReadOnlyCollection<SessionInfo> GetExpiredSessions(TimeSpan ttl)
    {
        var cutoff = DateTime.UtcNow - ttl;
        return _sessions.Values
            .Where(s => s.LastAccessedAt < cutoff)
            .ToList();
    }
}
