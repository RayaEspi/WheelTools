using System;
using System.Threading.Tasks;
using ECommons.EzIpcManager;

namespace WheelTools;

public static class SimpleWheelIpc
{
    private static readonly object Gate = new();
    private static bool _initialized;
    private static readonly Holder _holder = new();

    private sealed class Holder
    {
        [EzIPC]
        public Func<CreateGameMessage, Task<string>> CreateGameIPC;
    }

    private static void EnsureInit()
    {
        if (_initialized) return;
        lock (Gate)
        {
            if (_initialized) return;
            EzIPC.Init(_holder, "SimpleWheel", safeWrapper: SafeWrapper.AnyException, false);
            _initialized = true;
        }
    }
    public static Task<string> CreateGame(string title, int maxSpins, int speed, string theme, string preset, bool testGame)
    {
        EnsureInit();
        var fn = _holder.CreateGameIPC;
        if (fn == null)
            return Task.FromException<string>(new InvalidOperationException("SimpleWheel IPC provider not available."));

        return fn(new CreateGameMessage
        {
            Title = title,
            MaxSpins = maxSpins,
            Speed = speed,
            Theme = theme,
            Preset = preset,
            TestGame = testGame,
        });
    }

    public sealed class CreateGameMessage
    {
        public string Title { get; set; } = string.Empty;
        public int MaxSpins { get; set; }
        public int Speed { get; set; }
        public string Theme { get; set; } = string.Empty;
        public string Preset { get; set; } = string.Empty;
        public bool TestGame { get; set; }
    }
}
