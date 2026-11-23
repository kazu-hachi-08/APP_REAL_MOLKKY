using System;
using System.IO;
using UnityEngine;

namespace RealMolkky.Molkky
{
    /// <summary>
    /// 1回の投げの動きの記録。投げた端末で物理を計算しながら一定間隔で姿勢を残し、
    /// 他の端末はこれを再生するだけにする（フェーズ5でそのまま送受信に使う）。
    /// 並び: 0 = 棒、1〜12 = ピン番号。最後のフレームが最終位置。
    /// </summary>
    public sealed class ThrowRecord
    {
        const uint Magic = 0x4B4C4D52; // "RMLK"
        const byte Version = 1;
        const int FloatsPerPose = 7;   // 位置3 + 回転4

        public float FrameInterval { get; }
        public int BodyCount { get; }
        public int FrameCount => _frameCount;
        public float Duration => Mathf.Max(0, _frameCount - 1) * FrameInterval;

        float[] _data;
        int _frameCount;

        public ThrowRecord(float frameInterval, int bodyCount = MolkkySpec.BodyCount, int capacityFrames = 256)
        {
            if (frameInterval <= 0f) throw new ArgumentOutOfRangeException(nameof(frameInterval));
            FrameInterval = frameInterval;
            BodyCount = bodyCount;
            _data = new float[Math.Max(1, capacityFrames) * bodyCount * FloatsPerPose];
        }

        /// <summary>フレームを1つ追加し、その番号を返す。続けて <see cref="SetPose"/> で全物体を埋める。</summary>
        public int AddFrame()
        {
            int needed = (_frameCount + 1) * BodyCount * FloatsPerPose;
            if (needed > _data.Length) Array.Resize(ref _data, Math.Max(needed, _data.Length * 2));
            return _frameCount++;
        }

        public void SetPose(int frame, int body, Vector3 position, Quaternion rotation)
        {
            int i = Index(frame, body);
            _data[i] = position.x;
            _data[i + 1] = position.y;
            _data[i + 2] = position.z;
            _data[i + 3] = rotation.x;
            _data[i + 4] = rotation.y;
            _data[i + 5] = rotation.z;
            _data[i + 6] = rotation.w;
        }

        public void GetPose(int frame, int body, out Vector3 position, out Quaternion rotation)
        {
            int i = Index(frame, body);
            position = new Vector3(_data[i], _data[i + 1], _data[i + 2]);
            rotation = new Quaternion(_data[i + 3], _data[i + 4], _data[i + 5], _data[i + 6]);
        }

        /// <summary>時刻 t の姿勢（フレーム間は補間）</summary>
        public void Sample(float time, int body, out Vector3 position, out Quaternion rotation)
        {
            if (_frameCount == 0) throw new InvalidOperationException("記録が空です。");
            float f = Mathf.Clamp(time / FrameInterval, 0f, _frameCount - 1);
            int a = Mathf.FloorToInt(f);
            int b = Mathf.Min(a + 1, _frameCount - 1);
            GetPose(a, body, out var pa, out var ra);
            if (a == b)
            {
                position = pa;
                rotation = ra;
                return;
            }
            GetPose(b, body, out var pb, out var rb);
            float u = f - a;
            position = Vector3.LerpUnclamped(pa, pb, u);
            rotation = Quaternion.SlerpUnclamped(ra, rb, u);
        }

        int Index(int frame, int body)
        {
            if ((uint)frame >= (uint)_frameCount) throw new ArgumentOutOfRangeException(nameof(frame));
            if ((uint)body >= (uint)BodyCount) throw new ArgumentOutOfRangeException(nameof(body));
            return (frame * BodyCount + body) * FloatsPerPose;
        }

        // ---- 送受信用 ----
        // 今は float のまま。サイズが問題になればフェーズ5で量子化する。

        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Magic);
                w.Write(Version);
                w.Write(FrameInterval);
                w.Write((byte)BodyCount);
                w.Write(_frameCount);
                int n = _frameCount * BodyCount * FloatsPerPose;
                for (int i = 0; i < n; i++) w.Write(_data[i]);
            }
            return ms.ToArray();
        }

        public static ThrowRecord FromBytes(byte[] bytes)
        {
            using var r = new BinaryReader(new MemoryStream(bytes));
            if (r.ReadUInt32() != Magic) throw new InvalidDataException("投げの記録ではありません。");
            byte version = r.ReadByte();
            if (version != Version) throw new InvalidDataException($"未対応の記録バージョン: {version}");
            float interval = r.ReadSingle();
            int bodyCount = r.ReadByte();
            int frames = r.ReadInt32();
            if (frames < 0 || bodyCount <= 0) throw new InvalidDataException("記録が壊れています。");

            var rec = new ThrowRecord(interval, bodyCount, frames);
            int n = frames * bodyCount * FloatsPerPose;
            for (int i = 0; i < n; i++) rec._data[i] = r.ReadSingle();
            rec._frameCount = frames;
            return rec;
        }
    }
}
