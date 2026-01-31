using System.Collections.Concurrent;
using wenu.Entities;

namespace wenu.Configs
{
    /// <summary>
    /// Singleton, thread-safe, in-memory user store.
    /// All user data lives here — no database required.
    /// </summary>
    public class InMemoryUserStore
    {
        /// <summary>Primary store: userId → User</summary>
        private readonly ConcurrentDictionary<int, Users> _usersById = new();

        /// <summary>Secondary index: username → userId (case-insensitive)</summary>
        private readonly ConcurrentDictionary<string, int> _usernameIndex =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Secondary index: email → userId (case-insensitive)</summary>
        private readonly ConcurrentDictionary<string, int> _emailIndex =
            new(StringComparer.OrdinalIgnoreCase);

        private int _nextId = 0;

        // ─── ID generation ────────────────────────────────────────────

        public int GenerateId() => Interlocked.Increment(ref _nextId);

        // ─── Add ──────────────────────────────────────────────────────

        /// <returns>true if the user was added; false if the username or email already exists.</returns>
        public bool TryAdd(Users user)
        {
            // Try to claim the username first
            if (!_usernameIndex.TryAdd(user.UserName, user.Id))
                return false;

            // Try to claim the email
            if (!_emailIndex.TryAdd(user.Email, user.Id))
            {
                // Roll back the username claim
                _usernameIndex.TryRemove(user.UserName, out _);
                return false;
            }

            // Both indexes claimed — insert the user
            _usersById[user.Id] = user;
            return true;
        }

        // ─── Read ─────────────────────────────────────────────────────

        public Users? FindById(int id) =>
            _usersById.TryGetValue(id, out var user) ? user : null;

        public Users? FindByUsername(string username)
        {
            if (_usernameIndex.TryGetValue(username, out var id))
                return FindById(id);
            return null;
        }

        public Users? FindByEmail(string email)
        {
            if (_emailIndex.TryGetValue(email, out var id))
                return FindById(id);
            return null;
        }

        public bool ExistsUsername(string username) =>
            _usernameIndex.ContainsKey(username);

        public bool ExistsEmail(string email) =>
            _emailIndex.ContainsKey(email);

        // ─── Update ───────────────────────────────────────────────────

        /// <summary>
        /// Replaces the stored user object entirely.
        /// </summary>
        public void Update(Users user)
        {
            if (_usersById.ContainsKey(user.Id))
                _usersById[user.Id] = user;
        }

        // ─── Enumerate ────────────────────────────────────────────────

        public IEnumerable<Users> GetAll() => _usersById.Values;
    }
}