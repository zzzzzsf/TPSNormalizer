using System;
using System.Collections.Generic;

namespace StableGame
{
    public static class TimingMath
    {
        public static float FrameScale(float seconds, float referenceFps)
        {
            if (!FinitePositive(seconds) || !FinitePositive(referenceFps))
                return 0f;
            return seconds * referenceFps;
        }

        // Retain the same decay per second as a per-frame blend at referenceFps.
        public static float Blend(float alpha, float seconds, float referenceFps)
        {
            float steps = FrameScale(seconds, referenceFps);
            if (steps <= 0f) return 0f;
            if (float.IsNaN(alpha) || alpha <= 0f) return 0f;
            if (alpha >= 1f) return 1f;
            return (float)(1.0 - Math.Pow(1.0 - alpha, steps));
        }

        public static bool FinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static float RateBlend(float frameAlpha, float sourceStep, float seconds, float referenceFps)
        {
            if (!FinitePositive(sourceStep) || !FinitePositive(referenceFps)) return 0f;
            // Recover the rate BEFORE clamping; clamping rate*dt first loses
            // information at low FPS and causes instant ADS/free-look snaps.
            return Blend(frameAlpha / sourceStep / referenceFps, seconds, referenceFps);
        }

        public static float ReferenceVisualScale(int referenceFps, float fixedStep)
        {
            if (referenceFps <= 0 || !FinitePositive(fixedStep)) return 1f;
            double physicsPerRender = 1.0 / (fixedStep * referenceFps);
            return physicsPerRender < 1 ? (float)(2 - physicsPerRender) : 1f;
        }

        public static float MixedRateBlend(float rate, float seconds, int referenceFps, float fixedStep)
        {
            if (!FinitePositive(rate) || !FinitePositive(seconds) || !FinitePositive(fixedStep) || referenceFps <= 0) return 0f;
            // Reproduce the original reference-FPS mixed clock's decay, instead
            // of changing 120-FPS ADS speed while fixing other frame rates.
            double ratio = 1.0 / (fixedStep * referenceFps);
            double lowWeight, lowStep, highStep;
            if (ratio < 1)
            {
                lowWeight = 1 - ratio;
                lowStep = 1.0 / referenceFps;
                highStep = fixedStep;
            }
            else
            {
                double whole = Math.Floor(ratio + 1e-6);
                lowWeight = 1 - Math.Max(0, ratio - whole);
                lowStep = whole * fixedStep;
                highStep = (whole + 1) * fixedStep;
            }
            double low = Math.Max(0, 1 - rate * lowStep), high = Math.Max(0, 1 - rate * highStep);
            if ((low <= 0 && lowWeight > 1e-6) || (high <= 0 && 1 - lowWeight > 1e-6)) return 1f;
            double log = (lowWeight >= 1 - 1e-6 ? Math.Log(low) : lowWeight * Math.Log(low) + (1 - lowWeight) * Math.Log(high));
            return (float)(1 - Math.Exp(log * referenceFps * seconds));
        }

        private static double ClampPrimitive(double x)
        {
            return x <= 0 ? 0 : x < 1 ? x * x * 0.5 : x - 0.5;
        }

        // Exact area of the game's time-based ValueBlender over one physics
        // interval. Inputs before the ADS event do not receive a retroactive force.
        public static float BlenderArea(double from, double to, double startTime, float startValue, float speed, float target)
        {
            from = Math.Max(from, startTime);
            if (to <= from) return 0f;
            double slope = speed * (target >= 0.5f ? 1.0 : -1.0);
            if (Math.Abs(slope) < 1e-10) return (float)((to - from) * Math.Max(0, Math.Min(1, startValue)));
            double left = startValue + (from - startTime) * slope;
            double right = startValue + (to - startTime) * slope;
            return (float)((ClampPrimitive(right) - ClampPrimitive(left)) / slope);
        }

        // Sample the ORIGINAL FOV recurrence at the configured reference FPS,
        // using elapsed time instead of repeatedly feeding back render samples.
        public static float FovRemaining(double elapsed, float duration, int referenceFps)
        {
            if (!FinitePositive(duration) || referenceFps <= 0 || elapsed >= duration) return 0f;
            if (elapsed <= 0) return 1f;
            double frame = elapsed * referenceFps;
            long whole = (long)Math.Floor(frame);
            double fraction = frame - whole;
            double count = (double)duration * referenceFps;
            double remaining = 1;
            for (long k = 1; k <= whole; k++)
            {
                remaining *= Math.Max(0, 1 - k / count);
                if (remaining < 1e-12) return 0f;
            }
            return (float)(remaining * Math.Pow(Math.Max(0, 1 - (whole + 1) / count), fraction));
        }

        public static int ReferenceTicks(ref double carry, float seconds, int referenceFps)
        {
            if (!FinitePositive(seconds) || referenceFps <= 0) return 0;
            carry += seconds;
            int ticks = (int)Math.Min(int.MaxValue, Math.Floor(carry * referenceFps + 1e-6));
            carry = Math.Max(0, carry - (double)ticks / referenceFps);
            return ticks;
        }
    }

    // Each transition multiplies a completed reference tick only once. The
    // fractional tick still uses the exact same recurrence as FovRemaining.
    internal struct FovSampler
    {
        private float duration;
        private int reference;
        private long completed;
        private double product;
        private bool initialized;

        internal float Sample(double elapsed, float seconds, int fps)
        {
            if (!TimingMath.FinitePositive(seconds) || fps <= 0 || elapsed >= seconds) return 0;
            if (elapsed <= 0) return 1;
            double frame = elapsed * fps;
            long target = (long)Math.Floor(frame);
            if (!initialized || duration != seconds || reference != fps || target < completed)
            { duration = seconds; reference = fps; completed = 0; product = 1; initialized = true; }
            if (product < 1e-12) return 0;
            double count = (double)seconds * fps;
            for (long k = completed + 1; k <= target; k++)
            {
                product *= Math.Max(0, 1 - k / count);
                if (product < 1e-12) { completed = target; return 0; }
            }
            completed = target;
            return (float)(product * Math.Pow(Math.Max(0, 1 - (target + 1) / count), frame - target));
        }
    }

    public sealed class DurationAverage
    {
        private struct Sample { internal double Seconds; internal float Value; }
        private Sample[] samples = new Sample[256];
        private int first, count;
        private double seconds;
        public bool HasSamples { get { return count > 0; } }
        public void Clear() { first = count = 0; seconds = 0; }

        public void Add(float value, float duration)
        {
            if (!TimingMath.FinitePositive(duration) || float.IsNaN(value) || float.IsInfinity(value)) return;
            if (count == samples.Length)
            {
                var larger = new Sample[samples.Length * 2];
                for (int i = 0; i < count; i++) larger[i] = samples[(first + i) % samples.Length];
                samples = larger; first = 0;
            }
            samples[(first + count) % samples.Length] = new Sample { Seconds = duration, Value = value };
            count++;
            seconds += duration;
            // Retain the largest supported window, even when the user increases
            // the configured window during a raid (reference FPS 30..360).
            double excess = seconds - 10.0 / 30;
            while (excess > 0 && count > 0)
            {
                Sample sample = samples[first];
                double trim = Math.Min(excess, sample.Seconds);
                sample.Seconds -= trim;
                seconds -= trim;
                excess -= trim;
                if (sample.Seconds <= 1e-12) { first = (first + 1) % samples.Length; count--; }
                else samples[first] = sample;
            }
        }

        public float Get(double window)
        {
            double remaining = window, sum = 0, used = 0;
            for (int i = count - 1; i >= 0 && remaining > 0; i--)
            {
                Sample sample = samples[(first + i) % samples.Length];
                double weight = Math.Min(remaining, sample.Seconds);
                sum += sample.Value * weight;
                used += weight;
                remaining -= weight;
            }
            return used > 0 ? (float)(sum / used) : 0f;
        }
    }
}
