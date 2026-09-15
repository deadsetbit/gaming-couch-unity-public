using System;
using System.Text;

namespace DSB.GC
{
    internal static class GCPlayerSeed
    {
        internal const int MinSeed = 1;
        internal const int MaxSeed = 999999;

        internal static int FromPlayerName(string playerName)
        {
            return ToSeed(ComputeFnv1A32(NormalizePlayerName(playerName)));
        }

        internal static int NormalizeOrFallback(int playerSeed, string playerName, int fallbackIndex)
        {
            if (playerSeed >= MinSeed && playerSeed <= MaxSeed)
            {
                return playerSeed;
            }

            if (!string.IsNullOrWhiteSpace(playerName))
            {
                return FromPlayerName(playerName);
            }

            return ToSeed((uint)Math.Max(0, fallbackIndex));
        }

        internal static uint ComputeFnv1A32(string value)
        {
            return GCFnv1A32.Compute(value);
        }

        private static string NormalizePlayerName(string playerName)
        {
            return (playerName ?? string.Empty)
                .Normalize(NormalizationForm.FormKC)
                .Trim()
                .ToLowerInvariant();
        }

        private static int ToSeed(uint hash)
        {
            return (int)(hash % MaxSeed) + MinSeed;
        }
    }

    internal static class GCFnv1A32
    {
        internal static uint Compute(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                for (var index = 0; index < bytes.Length; index++)
                {
                    hash ^= bytes[index];
                    hash *= 16777619u;
                }

                return hash;
            }
        }
    }
}
