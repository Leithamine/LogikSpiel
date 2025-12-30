using System.Reflection;
using LogikSpiel.Services;

namespace LogikSpiel.Services;

public static class UserProfileServiceExtensions
{
    public static async Task<int> GetCoinsAsync(this IUserProfileService svc)
    {
        var user = await svc.GetUserAsync();
        return GetIntProp(user, "Coins");
    }

    public static async Task<bool> TrySpendCoinsAsync(this IUserProfileService svc, int cost)
    {
        if (cost <= 0) return true;

        var user = await svc.GetUserAsync();
        if (user is null) return false;

        int coins = GetIntProp(user, "Coins");
        if (coins < cost) return false;

        if (!SetIntProp(user, "Coins", coins - cost))
            return false;

        await SaveUserBestEffortAsync(svc, user);
        return true;
    }

    public static async Task AddCoinsAsync(this IUserProfileService svc, int delta)
    {
        if (delta == 0) return;

        var user = await svc.GetUserAsync();
        if (user is null) return;

        int coins = GetIntProp(user, "Coins");
        int next = Math.Max(0, coins + delta);

        if (!SetIntProp(user, "Coins", next))
            return;

        await SaveUserBestEffortAsync(svc, user);
    }

    private static int GetIntProp(object? obj, string propName)
    {
        if (obj is null) return 0;
        var p = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
        if (p is null) return 0;
        var v = p.GetValue(obj);
        return v is int i ? i : 0;
    }

    private static bool SetIntProp(object obj, string propName, int value)
    {
        var p = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
        if (p is null || !p.CanWrite) return false;
        p.SetValue(obj, value);
        return true;
    }

    private static async Task SaveUserBestEffortAsync(IUserProfileService svc, object user)
    {
        var t = svc.GetType();

        // typischerweise: SaveUserAsync(UserProfile), UpdateUserAsync(UserProfile), SetUserAsync(UserProfile)
        var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public);

        foreach (var m in methods)
        {
            var ps = m.GetParameters();
            if (ps.Length != 1) continue;

            // Parameter kompatibel?
            if (!ps[0].ParameterType.IsAssignableFrom(user.GetType())) continue;

            var name = m.Name.ToLowerInvariant();
            if (!(name.Contains("save") || name.Contains("update") || name.Contains("set"))) continue;

            var res = m.Invoke(svc, new[] { user });
            if (res is Task task) await task;
            return;
        }

        // Falls du gar keine Save-Methode hast, passiert hier einfach nichts.
        // Dann musst du in deinem IUserProfileService eine Save/Update Methode anbieten.
    }
}
