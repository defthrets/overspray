using System.Drawing;
using GTA;
using GTA.Native;

namespace Overspray.UI
{
    /// <summary>
    /// Where the paint is about to go.
    ///
    /// THE MIDDLE OF THE SCREEN IS NOT AN APPROXIMATION HERE, IT IS THE ANSWER. Surface.InFront
    /// probes from GameplayCamera.Position along GameplayCamera.Direction, and that ray leaves
    /// the screen through its exact centre. So a dot at 0.5, 0.5 is not "roughly where you are
    /// pointing" -- it is the pixel the probe goes through.
    ///
    /// It has to be drawn at all because the mod hides the weapon. With the extinguisher model
    /// invisible and a can in his hand the game has no reason to show you its own reticle, and
    /// free-aiming a thing you cannot see the aim point of is guesswork.
    /// </summary>
    internal static class Reticle
    {
        /// <summary>A dark backing, so a pale colour on a pale wall is still findable.</summary>
        private static readonly Color Shadow = Color.FromArgb(200, 0, 0, 0);

        public static void Draw(Color paint, bool aiming, bool spraying)
        {
            const float cx = 0.5f;
            const float cy = 0.5f;

            // Legible, not raw. Black paint would otherwise give a black dot, and the one
            // moment you most need to see the aim point is the one where you cannot.
            var c = Hud.Legible(paint);

            var dot = spraying ? 0.0044f : 0.0032f;
            var edge = 0.0015f;

            Hud.Rect(cx, cy, Hud.X(dot + edge * 2f), dot + edge * 2f, Shadow);
            Hud.Rect(cx, cy, Hud.X(dot), dot, c);

            // The ticks only while he is actually pointing at something. A permanent crosshair
            // sitting in the middle of the screen is something you stop seeing, and then it is
            // just clutter over the game.
            if (!aiming && !spraying) return;

            const float gap = 0.011f;
            const float len = 0.009f;
            const float thin = 0.0016f;

            var half = thin * 0.5f;

            // Above and below, then left and right. Each gets the same dark backing.
            Tick(cx - Hud.X(half), cy - gap - len, Hud.X(thin), len, c);
            Tick(cx - Hud.X(half), cy + gap, Hud.X(thin), len, c);
            Tick(cx - Hud.X(gap + len), cy - half, Hud.X(len), thin, c);
            Tick(cx + Hud.X(gap), cy - half, Hud.X(len), thin, c);
        }

        private static void Tick(float left, float top, float w, float h, Color c)
        {
            Hud.Box(left - 0.0008f, top - 0.0008f, w + 0.0016f, h + 0.0016f, Shadow);
            Hud.Box(left, top, w, h, c);
        }

        /// <summary>Whether he is looking down the sights rather than merely holding it.</summary>
        public static bool Aiming()
        {
            try
            {
                return Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING, Game.Player.Handle);
            }
            catch
            {
                return false;
            }
        }
    }
}
