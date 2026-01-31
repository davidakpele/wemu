using System.Collections.Concurrent;
using wenu.Entities;

namespace wenu.Configs
{
    public class InMemoryUserStore
    {
        private readonly ConcurrentDictionary<int, Users> _usersById = new();
        private readonly ConcurrentDictionary<string, int> _usernameIndex =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, int> _emailIndex =
            new(StringComparer.OrdinalIgnoreCase);

        private int _nextId = 0;

        public int GenerateId() => Interlocked.Increment(ref _nextId);
        public bool TryAdd(Users user)
        {
            if (!_usernameIndex.TryAdd(user.UserName, user.Id))
                return false;

            if (!_emailIndex.TryAdd(user.Email, user.Id))
            {
                _usernameIndex.TryRemove(user.UserName, out _);
                return false;
            }
            _usersById[user.Id] = user;
            return true;
        }
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

        public void Update(Users user)
        {
            if (_usersById.ContainsKey(user.Id))
                _usersById[user.Id] = user;
        }

        public IEnumerable<Users> GetAll() => _usersById.Values;
    }
}