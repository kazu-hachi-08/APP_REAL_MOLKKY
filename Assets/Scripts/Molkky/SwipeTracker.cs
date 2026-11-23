using System.Collections.Generic;
using UnityEngine;

namespace RealMolkky.Molkky
{
    /// <summary>
    /// 指（マウス）の位置を時刻つきで残し、離す直前の速さを求める。入力の種類には依存しない。
    /// </summary>
    public class SwipeTracker
    {
        /// <summary>これより古いサンプルは捨てる（秒）</summary>
        const double KeepSeconds = 1.0;

        readonly List<(double t, Vector2 p)> _samples = new List<(double, Vector2)>();

        public bool IsTracking { get; private set; }
        public Vector2 StartPosition { get; private set; }
        public Vector2 LastPosition => _samples.Count > 0 ? _samples[_samples.Count - 1].p : StartPosition;
        public int SampleCount => _samples.Count;

        public void Begin(double time, Vector2 position)
        {
            _samples.Clear();
            _samples.Add((time, position));
            StartPosition = position;
            IsTracking = true;
        }

        public void Add(double time, Vector2 position)
        {
            if (!IsTracking) return;
            var last = _samples[_samples.Count - 1];
            if (time <= last.t)
            {
                // 同じ時刻なら新しい位置で上書き
                _samples[_samples.Count - 1] = (last.t, position);
                return;
            }
            _samples.Add((time, position));
            while (_samples.Count > 2 && time - _samples[0].t > KeepSeconds) _samples.RemoveAt(0);
        }

        /// <summary>
        /// 離した位置を加えて追跡を終える。離すフレームで位置が変わっていなければ追加しない
        /// （指を離すだけのフレームを「止まっていた」と数えて速さが落ちるのを防ぐ）。
        /// </summary>
        public void End(double time, Vector2 position)
        {
            if (!IsTracking) return;
            if (position != LastPosition) Add(time, position);
            IsTracking = false;
        }

        public void Cancel()
        {
            IsTracking = false;
            _samples.Clear();
        }

        /// <summary>最後のサンプルから window 秒さかのぼった区間の平均速度（単位/秒）</summary>
        public Vector2 VelocityAtEnd(float window)
        {
            int n = _samples.Count;
            if (n < 2) return Vector2.zero;

            var end = _samples[n - 1];
            double from = end.t - window;
            int i = n - 2;
            // 区間に入っている一番古いサンプルまでさかのぼる。最低でも1つ前は使う
            while (i > 0 && _samples[i - 1].t >= from) i--;

            var start = _samples[i];
            double dt = end.t - start.t;
            if (dt <= 1e-6) return Vector2.zero;
            return (end.p - start.p) / (float)dt;
        }
    }
}
